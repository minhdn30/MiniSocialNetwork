using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Net;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.AuthServices
{
    public class RedisLoginRateLimitService : ILoginRateLimitService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly LoginSecurityOptions _options;
        private readonly string _keyPrefix;

        public RedisLoginRateLimitService(
            IConnectionMultiplexer redis,
            IOptions<LoginSecurityOptions> options,
            IConfiguration configuration)
        {
            _redis = redis;
            _options = NormalizeOptions(options.Value);
            _keyPrefix = (configuration["Redis:KeyPrefix"] ?? "cloudm").Trim();
        }

        public async Task EnforceLoginAllowedAsync(string email, string? ipAddress, DateTime nowUtc)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedIp = NormalizeIp(ipAddress);
            var database = _redis.GetDatabase();

            try
            {
                await EnsureNotLockedAsync(
                    database,
                    BuildEmailLockKey(normalizedEmail),
                    _options.LockMinutes,
                    "Too many failed login attempts for this account.");

                if (string.IsNullOrWhiteSpace(normalizedIp))
                {
                    return;
                }

                await EnsureNotLockedAsync(
                    database,
                    BuildIpLockKey(normalizedIp),
                    _options.LockMinutes,
                    "Too many failed login attempts from this network.");

                if (_options.MaxFailedAttemptsPerEmailIpWindow > 0)
                {
                    await EnsureNotLockedAsync(
                        database,
                        BuildEmailIpLockKey(normalizedEmail, normalizedIp),
                        _options.LockMinutes,
                        "Too many failed login attempts for this account from this network.");
                }
            }
            catch (RedisException)
            {
                // Fail-open for login availability when Redis is temporarily unavailable.
                return;
            }
        }

        public async Task RecordFailedAttemptAsync(string email, string? ipAddress, DateTime nowUtc)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedIp = NormalizeIp(ipAddress);
            var database = _redis.GetDatabase();

            try
            {
                var emailFailedCount = await IncrementFixedWindowCounterAsync(
                    database,
                    BuildEmailFailureCounterPrefix(normalizedEmail),
                    _options.EmailWindowMinutes,
                    nowUtc);

                if (emailFailedCount >= _options.MaxFailedAttemptsPerEmailWindow)
                {
                    await SetLockAsync(database, BuildEmailLockKey(normalizedEmail), _options.LockMinutes);
                }

                if (string.IsNullOrWhiteSpace(normalizedIp))
                {
                    return;
                }

                var ipFailedCount = await IncrementFixedWindowCounterAsync(
                    database,
                    BuildIpFailureCounterPrefix(normalizedIp),
                    _options.IpWindowMinutes,
                    nowUtc);

                if (ipFailedCount >= _options.MaxFailedAttemptsPerIpWindow)
                {
                    await SetLockAsync(database, BuildIpLockKey(normalizedIp), _options.LockMinutes);
                }

                if (_options.MaxFailedAttemptsPerEmailIpWindow > 0)
                {
                    var emailIpFailedCount = await IncrementFixedWindowCounterAsync(
                        database,
                        BuildEmailIpFailureCounterPrefix(normalizedEmail, normalizedIp),
                        _options.EmailIpWindowMinutes,
                        nowUtc);

                    if (emailIpFailedCount >= _options.MaxFailedAttemptsPerEmailIpWindow)
                    {
                        await SetLockAsync(database, BuildEmailIpLockKey(normalizedEmail, normalizedIp), _options.LockMinutes);
                    }
                }
            }
            catch (RedisException)
            {
                // Best-effort recording only; do not break login flow on Redis outage.
                return;
            }
        }

        public async Task ClearFailedAttemptsAsync(string email, string? ipAddress, DateTime nowUtc)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedIp = NormalizeIp(ipAddress);
            var database = _redis.GetDatabase();

            try
            {
                var keysToDelete = new List<RedisKey>
                {
                    BuildEmailLockKey(normalizedEmail),
                    BuildCurrentWindowKey(BuildEmailFailureCounterPrefix(normalizedEmail), _options.EmailWindowMinutes, nowUtc)
                };

                if (!string.IsNullOrWhiteSpace(normalizedIp) && _options.MaxFailedAttemptsPerEmailIpWindow > 0)
                {
                    keysToDelete.Add(BuildEmailIpLockKey(normalizedEmail, normalizedIp));
                    keysToDelete.Add(
                        BuildCurrentWindowKey(
                            BuildEmailIpFailureCounterPrefix(normalizedEmail, normalizedIp),
                            _options.EmailIpWindowMinutes,
                            nowUtc));
                }

                await database.KeyDeleteAsync(keysToDelete.ToArray());
            }
            catch (RedisException)
            {
                // Best-effort cleanup only; do not break login flow on Redis outage.
                return;
            }
        }

        private static async Task EnsureNotLockedAsync(
            IDatabase database,
            string lockKey,
            int lockMinutes,
            string lockMessage)
        {
            var ttl = await database.KeyTimeToLiveAsync(lockKey);

            if (ttl.HasValue && ttl.Value > TimeSpan.Zero)
            {
                var remainingSeconds = Math.Max(1, (int)Math.Ceiling(ttl.Value.TotalSeconds));
                throw new UnauthorizedException($"{lockMessage} Please wait {remainingSeconds} seconds and try again.");
            }

            if (ttl.HasValue)
            {
                await database.KeyDeleteAsync(lockKey);
                return;
            }

            var exists = await database.KeyExistsAsync(lockKey);
            if (!exists)
            {
                return;
            }

            var expires = TimeSpan.FromMinutes(Math.Max(1, lockMinutes));
            await database.KeyExpireAsync(lockKey, expires);
            var fallbackRemaining = Math.Max(1, (int)Math.Ceiling(expires.TotalSeconds));
            throw new UnauthorizedException($"{lockMessage} Please wait {fallbackRemaining} seconds and try again.");
        }

        private static async Task<long> IncrementFixedWindowCounterAsync(
            IDatabase database,
            string keyPrefix,
            int windowMinutes,
            DateTime nowUtc)
        {
            var windowSeconds = Math.Max(60, windowMinutes * 60);
            var nowUnix = new DateTimeOffset(nowUtc).ToUnixTimeSeconds();
            var bucket = nowUnix / windowSeconds;
            var key = $"{keyPrefix}:w:{bucket}";

            var count = await database.StringIncrementAsync(key);
            if (count == 1)
            {
                var ttlSeconds = Math.Max(1, (bucket + 1) * windowSeconds - nowUnix + 30);
                await database.KeyExpireAsync(key, TimeSpan.FromSeconds(ttlSeconds));
            }

            return count;
        }

        private static async Task SetLockAsync(IDatabase database, string key, int lockMinutes)
        {
            var expires = TimeSpan.FromMinutes(Math.Max(1, lockMinutes));
            await database.StringSetAsync(key, "1", expires);
        }

        private static string BuildCurrentWindowKey(string keyPrefix, int windowMinutes, DateTime nowUtc)
        {
            var windowSeconds = Math.Max(60, windowMinutes * 60);
            var nowUnix = new DateTimeOffset(nowUtc).ToUnixTimeSeconds();
            var bucket = nowUnix / windowSeconds;

            return $"{keyPrefix}:w:{bucket}";
        }

        private string BuildEmailLockKey(string email)
        {
            return $"{_keyPrefix}:auth:login:lock:email:{email}";
        }

        private string BuildIpLockKey(string ipAddress)
        {
            return $"{_keyPrefix}:auth:login:lock:ip:{ipAddress}";
        }

        private string BuildEmailIpLockKey(string email, string ipAddress)
        {
            return $"{_keyPrefix}:auth:login:lock:email-ip:{email}:{ipAddress}";
        }

        private string BuildEmailFailureCounterPrefix(string email)
        {
            return $"{_keyPrefix}:auth:login:fail:email:{email}";
        }

        private string BuildIpFailureCounterPrefix(string ipAddress)
        {
            return $"{_keyPrefix}:auth:login:fail:ip:{ipAddress}";
        }

        private string BuildEmailIpFailureCounterPrefix(string email, string ipAddress)
        {
            return $"{_keyPrefix}:auth:login:fail:email-ip:{email}:{ipAddress}";
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string? NormalizeIp(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return null;
            }

            var candidate = ipAddress.Split(',')[0].Trim();
            if (!IPAddress.TryParse(candidate, out var parsedIp))
            {
                return null;
            }

            if (parsedIp.IsIPv4MappedToIPv6)
            {
                parsedIp = parsedIp.MapToIPv4();
            }

            return parsedIp.ToString();
        }

        private static LoginSecurityOptions NormalizeOptions(LoginSecurityOptions options)
        {
            options.MaxFailedAttemptsPerEmailWindow = options.MaxFailedAttemptsPerEmailWindow <= 0
                ? 10
                : options.MaxFailedAttemptsPerEmailWindow;
            options.EmailWindowMinutes = options.EmailWindowMinutes <= 0 ? 15 : options.EmailWindowMinutes;
            options.MaxFailedAttemptsPerIpWindow = options.MaxFailedAttemptsPerIpWindow <= 0
                ? 50
                : options.MaxFailedAttemptsPerIpWindow;
            options.IpWindowMinutes = options.IpWindowMinutes <= 0 ? 15 : options.IpWindowMinutes;
            options.MaxFailedAttemptsPerEmailIpWindow = options.MaxFailedAttemptsPerEmailIpWindow < 0
                ? 5
                : options.MaxFailedAttemptsPerEmailIpWindow;
            options.EmailIpWindowMinutes = options.EmailIpWindowMinutes <= 0 ? 15 : options.EmailIpWindowMinutes;
            options.LockMinutes = options.LockMinutes <= 0 ? 15 : options.LockMinutes;

            return options;
        }
    }
}
