using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using CloudM.API.Hubs;
using CloudM.Application.DTOs.PresenceDTOs;
using CloudM.Application.Services.PresenceServices;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Infrastructure.Repositories.Presences;
using StackExchange.Redis;

namespace CloudM.API.Services
{
    public class OnlinePresenceService : IOnlinePresenceService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IAccountBlockRepository _accountBlockRepository;
        private readonly IOnlinePresenceRepository _onlinePresenceRepository;
        private readonly IHubContext<UserHub> _userHubContext;
        private readonly OnlinePresenceOptions _options;
        private readonly string _keyPrefix;
        private readonly ILogger<OnlinePresenceService> _logger;
        private readonly TimeSpan _countTtl;
        private readonly TimeSpan _connectionOwnerTtl;
        private readonly TimeSpan _offlineLockTtl;
        private readonly TimeSpan _lastDisconnectTtl;

        public OnlinePresenceService(
            IConnectionMultiplexer redis,
            IAccountBlockRepository accountBlockRepository,
            IOnlinePresenceRepository onlinePresenceRepository,
            IHubContext<UserHub> userHubContext,
            IOptions<OnlinePresenceOptions> options,
            IConfiguration configuration,
            ILogger<OnlinePresenceService> logger)
        {
            _redis = redis;
            _accountBlockRepository = accountBlockRepository;
            _onlinePresenceRepository = onlinePresenceRepository;
            _userHubContext = userHubContext;
            _options = (options.Value ?? new OnlinePresenceOptions()).Normalize();
            _keyPrefix = (configuration["Redis:KeyPrefix"] ?? "cloudm").Trim();
            _logger = logger;

            _countTtl = TimeSpan.FromSeconds(Math.Max(_options.HeartbeatTtlSeconds, _options.OfflineGraceSeconds + 30));
            _connectionOwnerTtl = TimeSpan.FromSeconds(Math.Max(_options.HeartbeatTtlSeconds * 3, 180));
            _offlineLockTtl = TimeSpan.FromSeconds(Math.Max(5, _options.OfflineLockSeconds));
            _lastDisconnectTtl = TimeSpan.FromHours(30);
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
                await database.KeyDeleteAsync(BuildPendingOfflineKey(accountId));

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
                    await database.SortedSetRemoveAsync(BuildOfflineCandidatesKey(), BuildOfflineCandidateMember(targetAccountId));
                    await database.KeyDeleteAsync(BuildPendingOfflineKey(targetAccountId));
                    return;
                }

                await database.StringSetAsync(countKey, "0", _countTtl);
                var normalizedNowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
                var graceSeconds = Math.Max(0, _options.OfflineGraceSeconds);
                if (graceSeconds > 0)
                {
                    await database.StringSetAsync(
                        BuildPendingOfflineKey(targetAccountId),
                        "1",
                        TimeSpan.FromSeconds(graceSeconds));
                }
                else
                {
                    await database.KeyDeleteAsync(BuildPendingOfflineKey(targetAccountId));
                }

                var disconnectAtUnixSeconds = new DateTimeOffset(normalizedNowUtc).ToUnixTimeSeconds();
                await database.StringSetAsync(
                    BuildLastDisconnectAtKey(targetAccountId),
                    disconnectAtUnixSeconds.ToString(),
                    _lastDisconnectTtl);

                var dueAtUnixSeconds = new DateTimeOffset(normalizedNowUtc.AddSeconds(graceSeconds)).ToUnixTimeSeconds();
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
                await database.KeyDeleteAsync(BuildPendingOfflineKey(accountId));
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
            var blockedTargetIds = await GetBlockedTargetIdsAsync(
                viewerAccountId,
                normalizedAccountIds,
                cancellationToken);

            var contactSet = await _onlinePresenceRepository.GetContactTargetIdsAsync(
                viewerAccountId,
                normalizedAccountIds,
                cancellationToken);

            var livePresenceMap = await GetLivePresenceMapAsync(accountStates.Select(x => x.AccountId).ToList());
            var oneDay = TimeSpan.FromDays(1);
            var items = new List<PresenceSnapshotItemResponse>(normalizedAccountIds.Count);

            foreach (var accountId in normalizedAccountIds)
            {
                if (blockedTargetIds.Contains(accountId))
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

                var livePresence = livePresenceMap.TryGetValue(accountId, out var value)
                    ? value
                    : (IsOnline: false, LastOnlineAtUtc: (DateTime?)null);
                var isOnline = livePresence.IsOnline;
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

                var effectiveLastOnlineAt = livePresence.LastOnlineAtUtc ?? state.LastOnlineAt;
                var shouldShowLastActive =
                    effectiveLastOnlineAt.HasValue &&
                    (nowUtc - effectiveLastOnlineAt.Value) < oneDay;

                items.Add(new PresenceSnapshotItemResponse
                {
                    AccountId = accountId,
                    CanShowStatus = shouldShowLastActive,
                    IsOnline = false,
                    LastOnlineAt = shouldShowLastActive ? effectiveLastOnlineAt : null
                });
            }

            return new PresenceSnapshotResponse { Items = items };
        }

        public async Task NotifyBlockedPairHiddenAsync(
            Guid currentId,
            Guid targetId,
            CancellationToken cancellationToken = default)
        {
            if (currentId == Guid.Empty || targetId == Guid.Empty || currentId == targetId)
            {
                return;
            }

            await _userHubContext.Clients.Users(new[] { currentId.ToString("D") })
                .SendAsync("UserPresenceHidden", new
                {
                    AccountId = targetId
                }, cancellationToken);

            await _userHubContext.Clients.Users(new[] { targetId.ToString("D") })
                .SendAsync("UserPresenceHidden", new
                {
                    AccountId = currentId
                }, cancellationToken);
        }

        public async Task NotifyVisibilityChangedAsync(
            Guid accountId,
            OnlineStatusVisibilityEnum previousVisibility,
            OnlineStatusVisibilityEnum currentVisibility,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            if (accountId == Guid.Empty || previousVisibility == currentVisibility)
            {
                return;
            }

            try
            {
                var normalizedNowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
                if (currentVisibility == OnlineStatusVisibilityEnum.NoOne)
                {
                    await BroadcastHiddenAsync(accountId, cancellationToken);
                    return;
                }

                var database = _redis.GetDatabase();
                if (await IsEffectivelyOnlineAsync(database, accountId))
                {
                    await BroadcastOnlineAsync(accountId, normalizedNowUtc, cancellationToken);
                    return;
                }

                var lastOnlineAt = await GetEffectiveLastOnlineAtAsync(database, accountId, cancellationToken);
                if (lastOnlineAt.HasValue && normalizedNowUtc - lastOnlineAt.Value < TimeSpan.FromDays(1))
                {
                    await BroadcastOfflineAsync(
                        accountId,
                        DateTime.SpecifyKind(lastOnlineAt.Value, DateTimeKind.Utc),
                        cancellationToken);
                    return;
                }

                await BroadcastHiddenAsync(accountId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Presence visibility-change broadcast failed for {AccountId}.", accountId);
            }
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
            var normalizedNowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
            var confirmedCandidates = new List<(Guid AccountId, RedisValue Member, DateTime LastOnlineAtUtc)>();

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

                if (await IsPendingOfflineAsync(database, accountId))
                {
                    continue;
                }

                var candidateLastOnlineAtUtc = ResolveCandidateLastOnlineAtUtc(
                    entry.Score,
                    normalizedNowUtc);
                var disconnectTimestamp = await ReadLastDisconnectAtAsync(database, accountId);
                if (disconnectTimestamp.HasValue)
                {
                    candidateLastOnlineAtUtc = disconnectTimestamp.Value;
                }
                confirmedCandidates.Add((accountId, entry.Element, candidateLastOnlineAtUtc));
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

            var accountLastOnlineMap = new Dictionary<Guid, DateTime>();
            foreach (var candidate in confirmedCandidates)
            {
                if (!accountLastOnlineMap.TryGetValue(candidate.AccountId, out var existingTimestamp)
                    || candidate.LastOnlineAtUtc > existingTimestamp)
                {
                    accountLastOnlineMap[candidate.AccountId] = candidate.LastOnlineAtUtc;
                }
            }

            foreach (var group in accountLastOnlineMap.GroupBy(x => x.Value))
            {
                try
                {
                    await _onlinePresenceRepository.UpdateLastOnlineAtAsync(
                        group.Select(x => x.Key).ToList(),
                        group.Key,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Presence offline worker failed persisting LastOnlineAt for {Count} accounts.", group.Count());
                }
            }

            foreach (var pair in accountLastOnlineMap)
            {
                try
                {
                    await BroadcastOfflineAsync(pair.Key, pair.Value, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Presence offline broadcast failed for {AccountId}.", pair.Key);
                }
            }

            try
            {
                await database.SortedSetRemoveAsync(
                    candidateKey,
                    confirmedCandidates.Select(x => x.Member).ToArray());

                var pendingKeys = accountLastOnlineMap.Keys
                    .Select(x => (RedisKey)BuildPendingOfflineKey(x))
                    .ToArray();
                if (pendingKeys.Length > 0)
                {
                    await database.KeyDeleteAsync(pendingKeys);
                }
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence offline worker failed cleaning processed candidates.");
            }

            return accountLastOnlineMap.Count;
        }

        private async Task<Dictionary<Guid, (bool IsOnline, DateTime? LastOnlineAtUtc)>> GetLivePresenceMapAsync(
            IReadOnlyCollection<Guid> accountIds)
        {
            var result = accountIds.ToDictionary(id => id, _ => (false, (DateTime?)null));
            if (accountIds.Count == 0)
            {
                return result;
            }

            var database = _redis.GetDatabase();
            try
            {
                var accountList = result.Keys.ToList();
                var countTasks = accountList
                    .Select(id => database.StringGetAsync(BuildCountKey(id)))
                    .ToArray();
                await Task.WhenAll(countTasks);

                var offlineAccountIds = new List<Guid>();
                for (var i = 0; i < accountList.Count; i++)
                {
                    var rawCount = countTasks[i].Result;
                    if (rawCount.HasValue && long.TryParse(rawCount.ToString(), out var count) && count > 0)
                    {
                        result[accountList[i]] = (true, null);
                        continue;
                    }

                    offlineAccountIds.Add(accountList[i]);
                }

                if (offlineAccountIds.Count == 0)
                {
                    return result;
                }

                var pendingTasks = offlineAccountIds
                    .Select(id => database.KeyExistsAsync(BuildPendingOfflineKey(id)))
                    .ToArray();
                await Task.WhenAll(pendingTasks);
                var nonPendingAccountIds = new List<Guid>();
                for (var i = 0; i < offlineAccountIds.Count; i++)
                {
                    var accountId = offlineAccountIds[i];
                    if (pendingTasks[i].Result)
                    {
                        result[accountId] = (true, null);
                        continue;
                    }

                    nonPendingAccountIds.Add(accountId);
                }

                if (nonPendingAccountIds.Count == 0)
                {
                    return result;
                }

                var disconnectTasks = nonPendingAccountIds
                    .Select(id => database.StringGetAsync(BuildLastDisconnectAtKey(id)))
                    .ToArray();
                await Task.WhenAll(disconnectTasks);

                for (var i = 0; i < nonPendingAccountIds.Count; i++)
                {
                    var accountId = nonPendingAccountIds[i];
                    result[accountId] = (false, ParseUnixSecondsToUtc(disconnectTasks[i].Result));
                }
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence live-state lookup failed. Falling back to cached state.");
            }

            return result;
        }

        private async Task<HashSet<Guid>> GetBlockedTargetIdsAsync(
            Guid currentId,
            IReadOnlyCollection<Guid> targetIds,
            CancellationToken cancellationToken = default)
        {
            if (currentId == Guid.Empty || targetIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            var relations = await _accountBlockRepository.GetRelationsAsync(
                currentId,
                targetIds,
                cancellationToken);

            return relations
                .Where(x => x.IsBlockedEitherWay)
                .Select(x => x.TargetId)
                .ToHashSet();
        }

        private async Task<List<Guid>> FilterAudienceByBlockAsync(
            Guid accountId,
            List<Guid> audienceAccountIds,
            CancellationToken cancellationToken = default)
        {
            if (accountId == Guid.Empty || audienceAccountIds.Count == 0)
            {
                return audienceAccountIds;
            }

            var blockedTargetIds = await GetBlockedTargetIdsAsync(
                accountId,
                audienceAccountIds,
                cancellationToken);

            if (blockedTargetIds.Count == 0)
            {
                return audienceAccountIds;
            }

            return audienceAccountIds
                .Where(x => !blockedTargetIds.Contains(x))
                .Distinct()
                .ToList();
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

        private async Task<bool> IsEffectivelyOnlineAsync(IDatabase database, Guid accountId)
        {
            var onlineCount = await ReadOnlineCountAsync(database, accountId);
            if (onlineCount > 0)
            {
                return true;
            }

            return await IsPendingOfflineAsync(database, accountId);
        }

        private async Task<bool> IsPendingOfflineAsync(IDatabase database, Guid accountId)
        {
            try
            {
                return await database.KeyExistsAsync(BuildPendingOfflineKey(accountId));
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence pending-offline check failed for {AccountId}.", accountId);
                return false;
            }
        }

        private async Task<DateTime?> ReadLastDisconnectAtAsync(IDatabase database, Guid accountId)
        {
            try
            {
                var rawValue = await database.StringGetAsync(BuildLastDisconnectAtKey(accountId));
                return ParseUnixSecondsToUtc(rawValue);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Presence last-disconnect lookup failed for {AccountId}.", accountId);
                return null;
            }
        }

        private async Task<DateTime?> GetEffectiveLastOnlineAtAsync(
            IDatabase database,
            Guid accountId,
            CancellationToken cancellationToken)
        {
            var lastDisconnectAt = await ReadLastDisconnectAtAsync(database, accountId);
            if (lastDisconnectAt.HasValue)
            {
                return lastDisconnectAt.Value;
            }

            return await _onlinePresenceRepository.GetLastOnlineAtAsync(accountId, cancellationToken);
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
            audience = await FilterAudienceByBlockAsync(accountId, audience, cancellationToken);
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
            audience = await FilterAudienceByBlockAsync(accountId, audience, cancellationToken);
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

        private async Task BroadcastHiddenAsync(Guid accountId, CancellationToken cancellationToken)
        {
            var audience = await _onlinePresenceRepository.GetAudienceAccountIdsAsync(accountId, cancellationToken);
            audience = await FilterAudienceByBlockAsync(accountId, audience, cancellationToken);
            if (audience.Count == 0)
            {
                return;
            }

            await _userHubContext.Clients.Users(audience.Select(x => x.ToString()))
                .SendAsync("UserPresenceHidden", new
                {
                    AccountId = accountId
                }, cancellationToken);
        }

        private DateTime ResolveCandidateLastOnlineAtUtc(double dueAtScore, DateTime nowUtc)
        {
            long dueAtUnixSeconds;
            try
            {
                dueAtUnixSeconds = Convert.ToInt64(Math.Floor(dueAtScore));
            }
            catch
            {
                dueAtUnixSeconds = new DateTimeOffset(nowUtc).ToUnixTimeSeconds();
            }

            var disconnectAtUtc = DateTimeOffset
                .FromUnixTimeSeconds(dueAtUnixSeconds)
                .UtcDateTime
                .AddSeconds(-_options.OfflineGraceSeconds);

            if (disconnectAtUtc > nowUtc)
            {
                return nowUtc;
            }

            return disconnectAtUtc;
        }

        private static DateTime? ParseUnixSecondsToUtc(RedisValue rawValue)
        {
            if (!rawValue.HasValue)
            {
                return null;
            }

            if (!long.TryParse(rawValue.ToString(), out var unixSeconds))
            {
                return null;
            }

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            }
            catch
            {
                return null;
            }
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

        private string BuildPendingOfflineKey(Guid accountId)
        {
            return $"{_keyPrefix}:presence:pending-offline:{accountId:D}";
        }

        private string BuildLastDisconnectAtKey(Guid accountId)
        {
            return $"{_keyPrefix}:presence:last-disconnect-at:{accountId:D}";
        }

        private string BuildSnapshotRateLimitKey(Guid accountId, long bucket)
        {
            return $"{_keyPrefix}:presence:snapshot:rl:{accountId:D}:w:{bucket}";
        }
    }
}
