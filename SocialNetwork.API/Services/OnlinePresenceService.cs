using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SocialNetwork.API.Hubs;
using SocialNetwork.Application.DTOs.PresenceDTOs;
using SocialNetwork.Application.Services.PresenceServices;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Presences;
using StackExchange.Redis;

namespace SocialNetwork.API.Services
{
    public class OnlinePresenceService : IOnlinePresenceService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IOnlinePresenceRepository _onlinePresenceRepository;
        private readonly IHubContext<UserHub> _userHubContext;
        private readonly OnlinePresenceOptions _options;
        private readonly string _keyPrefix;
        private readonly ILogger<OnlinePresenceService> _logger;
        private readonly TimeSpan _countTtl;
        private readonly TimeSpan _connectionOwnerTtl;
        private readonly TimeSpan _offlineLockTtl;

        public OnlinePresenceService(
            IConnectionMultiplexer redis,
            IOnlinePresenceRepository onlinePresenceRepository,
            IHubContext<UserHub> userHubContext,
            IOptions<OnlinePresenceOptions> options,
            IConfiguration configuration,
            ILogger<OnlinePresenceService> logger)
        {
            _redis = redis;
            _onlinePresenceRepository = onlinePresenceRepository;
            _userHubContext = userHubContext;
            _options = (options.Value ?? new OnlinePresenceOptions()).Normalize();
            _keyPrefix = (configuration["Redis:KeyPrefix"] ?? "cloudm").Trim();
            _logger = logger;

            _countTtl = TimeSpan.FromSeconds(Math.Max(_options.HeartbeatTtlSeconds, _options.OfflineGraceSeconds + 30));
            _connectionOwnerTtl = TimeSpan.FromSeconds(Math.Max(_options.HeartbeatTtlSeconds * 3, 180));
            _offlineLockTtl = TimeSpan.FromSeconds(Math.Max(5, _options.OfflineLockSeconds));
        }

        public async Task MarkConnectedAsync(Guid accountId, string connectionId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            if (accountId == Guid.Empty || string.IsNullOrWhiteSpace(connectionId))
            {
                return;
            }

            var database = _redis.GetDatabase();
            try
            {
                await database.StringSetAsync(BuildConnectionOwnerKey(connectionId), accountId.ToString("D"), _connectionOwnerTtl);

                var count = await database.StringIncrementAsync(BuildCountKey(accountId));
                await database.KeyExpireAsync(BuildCountKey(accountId), _countTtl);
                await database.SortedSetRemoveAsync(BuildOfflineCandidatesKey(), BuildOfflineCandidateMember(accountId));

                if (count == 1)
                {
                    await BroadcastOnlineAsync(accountId, nowUtc, cancellationToken);
                }
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence connect update failed for {AccountId}.", accountId);
            }
        }

        public async Task MarkDisconnectedAsync(Guid? accountId, string connectionId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return;
            }

            var database = _redis.GetDatabase();
            try
            {
                var resolvedAccountId = accountId;
                var ownerKey = BuildConnectionOwnerKey(connectionId);

                if (!resolvedAccountId.HasValue || resolvedAccountId.Value == Guid.Empty)
                {
                    var rawOwner = await database.StringGetAsync(ownerKey);
                    if (rawOwner.HasValue && Guid.TryParse(rawOwner.ToString(), out var parsedOwner))
                    {
                        resolvedAccountId = parsedOwner;
                    }
                }

                await database.KeyDeleteAsync(ownerKey);

                if (!resolvedAccountId.HasValue || resolvedAccountId.Value == Guid.Empty)
                {
                    return;
                }

                var targetAccountId = resolvedAccountId.Value;
                var countKey = BuildCountKey(targetAccountId);
                var countAfterDecrement = await database.StringDecrementAsync(countKey);
                if (countAfterDecrement > 0)
                {
                    await database.KeyExpireAsync(countKey, _countTtl);
                    return;
                }

                await database.StringSetAsync(countKey, "0", _countTtl);
                var dueAtUnixSeconds = new DateTimeOffset(nowUtc.AddSeconds(_options.OfflineGraceSeconds)).ToUnixTimeSeconds();
                await database.SortedSetAddAsync(
                    BuildOfflineCandidatesKey(),
                    BuildOfflineCandidateMember(targetAccountId),
                    dueAtUnixSeconds);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence disconnect update failed for {ConnectionId}.", connectionId);
            }
        }

        public async Task TouchHeartbeatAsync(Guid accountId, string connectionId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            if (accountId == Guid.Empty || string.IsNullOrWhiteSpace(connectionId))
            {
                return;
            }

            var database = _redis.GetDatabase();
            try
            {
                await database.StringSetAsync(BuildConnectionOwnerKey(connectionId), accountId.ToString("D"), _connectionOwnerTtl);

                var countKey = BuildCountKey(accountId);
                var rawCount = await database.StringGetAsync(countKey);

                if (!rawCount.HasValue || !long.TryParse(rawCount.ToString(), out var countValue) || countValue <= 0)
                {
                    await database.StringSetAsync(countKey, "1", _countTtl);
                }
                else
                {
                    await database.KeyExpireAsync(countKey, _countTtl);
                }

                await database.SortedSetRemoveAsync(BuildOfflineCandidatesKey(), BuildOfflineCandidateMember(accountId));
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence heartbeat failed for {AccountId}.", accountId);
            }
        }

        public async Task<(bool Allowed, int RetryAfterSeconds)> TryConsumeSnapshotRateLimitAsync(
            Guid viewerAccountId,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            if (viewerAccountId == Guid.Empty)
            {
                return (false, 1);
            }

            var windowSeconds = Math.Max(10, _options.SnapshotRateLimitWindowSeconds);
            var maxRequests = Math.Max(1, _options.SnapshotRateLimitMaxRequests);
            var nowUnix = new DateTimeOffset(nowUtc).ToUnixTimeSeconds();
            var bucket = nowUnix / windowSeconds;
            var key = BuildSnapshotRateLimitKey(viewerAccountId, bucket);

            var database = _redis.GetDatabase();
            try
            {
                var requestCount = await database.StringIncrementAsync(key);
                if (requestCount == 1)
                {
                    await database.KeyExpireAsync(key, TimeSpan.FromSeconds(windowSeconds + 5));
                }

                if (requestCount <= maxRequests)
                {
                    return (true, 0);
                }

                var ttl = await database.KeyTimeToLiveAsync(key);
                var retryAfterSeconds = ttl.HasValue
                    ? Math.Max(1, (int)Math.Ceiling(ttl.Value.TotalSeconds))
                    : windowSeconds;
                return (false, retryAfterSeconds);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence snapshot rate-limit check failed open for {AccountId}.", viewerAccountId);
                return (true, 0);
            }
        }

        public async Task<PresenceSnapshotResponse> GetSnapshotAsync(
            Guid viewerAccountId,
            IReadOnlyCollection<Guid> accountIds,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            var normalizedAccountIds = (accountIds ?? Array.Empty<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedAccountIds.Count == 0)
            {
                return new PresenceSnapshotResponse();
            }

            var accountStates = await _onlinePresenceRepository.GetSnapshotAccountStatesAsync(
                normalizedAccountIds,
                cancellationToken);

            var stateMap = accountStates.ToDictionary(x => x.AccountId);

            var contactSet = await _onlinePresenceRepository.GetContactTargetIdsAsync(
                viewerAccountId,
                normalizedAccountIds,
                cancellationToken);

            var onlineMap = await GetOnlineMapAsync(accountStates.Select(x => x.AccountId).ToList());
            var oneDay = TimeSpan.FromDays(1);
            var items = new List<PresenceSnapshotItemResponse>(normalizedAccountIds.Count);

            foreach (var accountId in normalizedAccountIds)
            {
                if (!stateMap.TryGetValue(accountId, out var state))
                {
                    items.Add(new PresenceSnapshotItemResponse
                    {
                        AccountId = accountId,
                        CanShowStatus = false,
                        IsOnline = false,
                        LastOnlineAt = null
                    });
                    continue;
                }

                var canView = accountId == viewerAccountId
                    || (state.Visibility == OnlineStatusVisibilityEnum.ContactsOnly && contactSet.Contains(accountId));

                if (state.Visibility == OnlineStatusVisibilityEnum.NoOne && accountId != viewerAccountId)
                {
                    canView = false;
                }

                if (!canView)
                {
                    items.Add(new PresenceSnapshotItemResponse
                    {
                        AccountId = accountId,
                        CanShowStatus = false,
                        IsOnline = false,
                        LastOnlineAt = null
                    });
                    continue;
                }

                var isOnline = onlineMap.TryGetValue(accountId, out var online) && online;
                if (isOnline)
                {
                    items.Add(new PresenceSnapshotItemResponse
                    {
                        AccountId = accountId,
                        CanShowStatus = true,
                        IsOnline = true,
                        LastOnlineAt = null
                    });
                    continue;
                }

                var shouldShowLastActive =
                    state.LastOnlineAt.HasValue &&
                    (nowUtc - state.LastOnlineAt.Value) < oneDay;

                items.Add(new PresenceSnapshotItemResponse
                {
                    AccountId = accountId,
                    CanShowStatus = shouldShowLastActive,
                    IsOnline = false,
                    LastOnlineAt = shouldShowLastActive ? state.LastOnlineAt : null
                });
            }

            return new PresenceSnapshotResponse { Items = items };
        }

        public async Task<int> ProcessOfflineCandidatesAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken = default)
        {
            var safeBatchSize = batchSize <= 0 ? _options.WorkerBatchSize : batchSize;
            var database = _redis.GetDatabase();
            var candidateKey = BuildOfflineCandidatesKey();
            var nowUnix = new DateTimeOffset(nowUtc).ToUnixTimeSeconds();

            SortedSetEntry[] dueEntries;
            try
            {
                dueEntries = await database.SortedSetRangeByScoreWithScoresAsync(
                    candidateKey,
                    stop: nowUnix,
                    order: Order.Ascending,
                    take: safeBatchSize);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence offline worker failed to load candidates.");
                return 0;
            }

            if (dueEntries.Length == 0)
            {
                return 0;
            }

            var staleMembersToRemove = new List<RedisValue>();
            var confirmedCandidates = new List<(Guid AccountId, RedisValue Member)>();

            foreach (var entry in dueEntries)
            {
                var memberRaw = entry.Element.ToString();
                if (!Guid.TryParse(memberRaw, out var accountId) || accountId == Guid.Empty)
                {
                    staleMembersToRemove.Add(entry.Element);
                    continue;
                }

                var onlineCount = await ReadOnlineCountAsync(database, accountId);
                if (onlineCount > 0)
                {
                    staleMembersToRemove.Add(entry.Element);
                    continue;
                }

                bool lockAcquired;
                try
                {
                    lockAcquired = await database.StringSetAsync(
                        BuildOfflineLockKey(accountId),
                        "1",
                        _offlineLockTtl,
                        When.NotExists);
                }
                catch (RedisException ex)
                {
                    _logger.LogWarning(ex, "Presence offline worker lock failed for {AccountId}.", accountId);
                    continue;
                }

                if (!lockAcquired)
                {
                    continue;
                }

                onlineCount = await ReadOnlineCountAsync(database, accountId);
                if (onlineCount > 0)
                {
                    staleMembersToRemove.Add(entry.Element);
                    continue;
                }

                confirmedCandidates.Add((accountId, entry.Element));
            }

            if (staleMembersToRemove.Count > 0)
            {
                try
                {
                    await database.SortedSetRemoveAsync(candidateKey, staleMembersToRemove.ToArray());
                }
                catch (RedisException ex)
                {
                    _logger.LogWarning(ex, "Presence offline worker failed removing stale candidates.");
                }
            }

            if (confirmedCandidates.Count == 0)
            {
                return 0;
            }

            var confirmedIds = confirmedCandidates
                .Select(x => x.AccountId)
                .Distinct()
                .ToList();

            var nowTimestamp = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
            List<Guid> updatedAccountIds;
            try
            {
                updatedAccountIds = await _onlinePresenceRepository.UpdateLastOnlineAtAsync(
                    confirmedIds,
                    nowTimestamp,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Presence offline worker failed persisting LastOnlineAt.");
                return 0;
            }

            foreach (var accountId in updatedAccountIds)
            {
                try
                {
                    await BroadcastOfflineAsync(accountId, nowTimestamp, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Presence offline broadcast failed for {AccountId}.", accountId);
                }
            }

            try
            {
                await database.SortedSetRemoveAsync(
                    candidateKey,
                    confirmedCandidates.Select(x => x.Member).ToArray());
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence offline worker failed cleaning processed candidates.");
            }

            return updatedAccountIds.Count;
        }

        private async Task<Dictionary<Guid, bool>> GetOnlineMapAsync(IReadOnlyCollection<Guid> accountIds)
        {
            var result = accountIds.ToDictionary(id => id, _ => false);
            if (accountIds.Count == 0)
            {
                return result;
            }

            var database = _redis.GetDatabase();
            try
            {
                var accountList = accountIds.ToList();
                var countTasks = accountList
                    .Select(id => database.StringGetAsync(BuildCountKey(id)))
                    .ToArray();
                await Task.WhenAll(countTasks);

                for (var i = 0; i < accountList.Count; i++)
                {
                    var rawCount = countTasks[i].Result;
                    if (!rawCount.HasValue || !long.TryParse(rawCount.ToString(), out var count))
                    {
                        continue;
                    }

                    result[accountList[i]] = count > 0;
                }
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence online-map lookup failed. Falling back to offline.");
            }

            return result;
        }

        private async Task<long> ReadOnlineCountAsync(IDatabase database, Guid accountId)
        {
            try
            {
                var rawCount = await database.StringGetAsync(BuildCountKey(accountId));
                if (!rawCount.HasValue || !long.TryParse(rawCount.ToString(), out var count))
                {
                    return 0;
                }

                return Math.Max(0, count);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence count read failed for {AccountId}.", accountId);
                return 0;
            }
        }

        private async Task BroadcastOnlineAsync(Guid accountId, DateTime occurredAtUtc, CancellationToken cancellationToken)
        {
            var visibility = (await _onlinePresenceRepository.GetOnlineStatusVisibilityAsync(accountId, cancellationToken))
                ?? OnlineStatusVisibilityEnum.ContactsOnly;
            if (visibility == OnlineStatusVisibilityEnum.NoOne)
            {
                return;
            }

            var audience = await _onlinePresenceRepository.GetAudienceAccountIdsAsync(accountId, cancellationToken);
            if (audience.Count == 0)
            {
                return;
            }

            await _userHubContext.Clients.Users(audience.Select(x => x.ToString()))
                .SendAsync("UserOnline", new
                {
                    AccountId = accountId,
                    OccurredAt = occurredAtUtc
                }, cancellationToken);
        }

        private async Task BroadcastOfflineAsync(Guid accountId, DateTime lastOnlineAtUtc, CancellationToken cancellationToken)
        {
            var visibility = (await _onlinePresenceRepository.GetOnlineStatusVisibilityAsync(accountId, cancellationToken))
                ?? OnlineStatusVisibilityEnum.ContactsOnly;
            if (visibility == OnlineStatusVisibilityEnum.NoOne)
            {
                return;
            }

            var audience = await _onlinePresenceRepository.GetAudienceAccountIdsAsync(accountId, cancellationToken);
            if (audience.Count == 0)
            {
                return;
            }

            await _userHubContext.Clients.Users(audience.Select(x => x.ToString()))
                .SendAsync("UserOffline", new
                {
                    AccountId = accountId,
                    LastOnlineAt = lastOnlineAtUtc
                }, cancellationToken);
        }

        private string BuildCountKey(Guid accountId)
        {
            return $"{_keyPrefix}:presence:count:{accountId:D}";
        }

        private string BuildConnectionOwnerKey(string connectionId)
        {
            return $"{_keyPrefix}:presence:conn-owner:{connectionId}";
        }

        private string BuildOfflineCandidatesKey()
        {
            return $"{_keyPrefix}:presence:offline:candidates";
        }

        private static string BuildOfflineCandidateMember(Guid accountId)
        {
            return accountId.ToString("D");
        }

        private string BuildOfflineLockKey(Guid accountId)
        {
            return $"{_keyPrefix}:presence:lock:lastactive:{accountId:D}";
        }

        private string BuildSnapshotRateLimitKey(Guid accountId, long bucket)
        {
            return $"{_keyPrefix}:presence:snapshot:rl:{accountId:D}:w:{bucket}";
        }
    }
}
