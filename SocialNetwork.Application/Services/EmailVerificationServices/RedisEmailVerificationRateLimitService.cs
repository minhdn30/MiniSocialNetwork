using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Net;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.EmailVerificationServices
{
    public class RedisEmailVerificationRateLimitService : IEmailVerificationRateLimitService
    {
        private const int SlidingDayWindowSeconds = 24 * 60 * 60;

        private const string SlidingWindowScript = """
            local key = KEYS[1]
            local now = tonumber(ARGV[1])
            local window = tonumber(ARGV[2])
            local max = tonumber(ARGV[3])
            local member = tostring(ARGV[1]) .. '-' .. ARGV[4]

            redis.call('ZREMRANGEBYSCORE', key, '-inf', now - window)
            local count = redis.call('ZCARD', key)

            if count >= max then
                local oldest = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
                local retry = window
                if oldest[2] then
                    retry = window - (now - tonumber(oldest[2]))
                end

                if retry < 1 then
                    retry = 1
                end

                return {0, count, retry}
            end

            redis.call('ZADD', key, now, member)
            redis.call('EXPIRE', key, window + 300)
            return {1, count + 1, 0}
            """;

        private readonly IConnectionMultiplexer _redis;
        private readonly EmailVerificationSecurityOptions _options;
        private readonly string _keyPrefix;

        public RedisEmailVerificationRateLimitService(
            IConnectionMultiplexer redis,
            IOptions<EmailVerificationSecurityOptions> options,
            IConfiguration configuration)
        {
            _redis = redis;
            _options = NormalizeOptions(options.Value);
            _keyPrefix = (configuration["Redis:KeyPrefix"] ?? "cloudm").Trim();
        }

        public async Task EnforceSendRateLimitAsync(string email, string? ipAddress, DateTime nowUtc)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedIp = NormalizeIp(ipAddress);
            var database = _redis.GetDatabase();

            try
            {
                await EnforceCooldownAsync(
                    database,
                    $"{_keyPrefix}:verify:cooldown:email:{normalizedEmail}",
                    _options.ResendCooldownSeconds);

                await EnforceFixedWindowAsync(
                    database,
                    $"{_keyPrefix}:verify:global",
                    _options.GlobalSendWindowMinutes,
                    _options.MaxGlobalSendsPerWindow,
                    nowUtc,
                    "OTP system is busy. Please retry later.");

                await EnforceFixedWindowAsync(
                    database,
                    $"{_keyPrefix}:verify:email:{normalizedEmail}",
                    _options.SendWindowMinutes,
                    _options.MaxSendsPerWindow,
                    nowUtc,
                    "You have reached the OTP request limit for this email.");

                await EnforceSlidingWindowAsync(
                    database,
                    $"{_keyPrefix}:verify:email:{normalizedEmail}:day",
                    SlidingDayWindowSeconds,
                    _options.MaxSendsPerDay,
                    nowUtc,
                    "You have reached the OTP request limit for this email in the last 24 hours.");

                if (string.IsNullOrWhiteSpace(normalizedIp))
                {
                    return;
                }

                await EnforceFixedWindowAsync(
                    database,
                    $"{_keyPrefix}:verify:ip:{normalizedIp}",
                    _options.IpSendWindowMinutes,
                    _options.MaxSendsPerIpWindow,
                    nowUtc,
                    "Too many OTP requests from this network.");

                await EnforceSlidingWindowAsync(
                    database,
                    $"{_keyPrefix}:verify:ip:{normalizedIp}:day",
                    SlidingDayWindowSeconds,
                    _options.MaxSendsPerIpDay,
                    nowUtc,
                    "This network has reached the OTP request limit in the last 24 hours.");

                await EnforceFixedWindowAsync(
                    database,
                    $"{_keyPrefix}:verify:email-ip:{normalizedEmail}:{normalizedIp}",
                    _options.EmailIpSendWindowMinutes,
                    _options.MaxSendsPerEmailIpWindow,
                    nowUtc,
                    "Too many OTP requests for this email from this network.");
            }
            catch (RedisException)
            {
                throw new InternalServerException("OTP service is temporarily unavailable. Please try again shortly.");
            }
        }

        private static async Task EnforceCooldownAsync(
            IDatabase database,
            string cooldownKey,
            int cooldownSeconds)
        {
            if (cooldownSeconds <= 0)
            {
                return;
            }

            var set = await database.StringSetAsync(
                cooldownKey,
                "1",
                expiry: TimeSpan.FromSeconds(cooldownSeconds),
                when: When.NotExists);

            if (set)
            {
                return;
            }

            var ttl = await database.KeyTimeToLiveAsync(cooldownKey);
            var remainingSeconds = ttl.HasValue
                ? Math.Max(1, (int)Math.Ceiling(ttl.Value.TotalSeconds))
                : cooldownSeconds;
            throw new BadRequestException($"Please wait {remainingSeconds} seconds before requesting another code.");
        }

        private static async Task EnforceFixedWindowAsync(
            IDatabase database,
            string keyPrefix,
            int windowMinutes,
            int maxRequests,
            DateTime nowUtc,
            string limitMessage)
        {
            if (maxRequests <= 0)
            {
                return;
            }

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

            if (count <= maxRequests)
            {
                return;
            }

            var remaining = Math.Max(1, (int)((bucket + 1) * windowSeconds - nowUnix));
            throw new BadRequestException($"{limitMessage} Please wait {remaining} seconds.");
        }

        private static async Task EnforceSlidingWindowAsync(
            IDatabase database,
            string key,
            int windowSeconds,
            int maxRequests,
            DateTime nowUtc,
            string limitMessage)
        {
            if (maxRequests <= 0)
            {
                return;
            }

            var nowUnix = new DateTimeOffset(nowUtc).ToUnixTimeSeconds();
            var result = await database.ScriptEvaluateAsync(
                SlidingWindowScript,
                new RedisKey[] { key },
                new RedisValue[]
                {
                    nowUnix,
                    windowSeconds,
                    maxRequests,
                    Guid.NewGuid().ToString("N")
                });

            var values = (RedisResult[]?)result;
            if (values == null || values.Length < 3)
            {
                throw new InternalServerException("OTP service is temporarily unavailable. Please try again shortly.");
            }

            var isAllowed = ParseRedisLong(values[0]) == 1;
            if (isAllowed)
            {
                return;
            }

            var remaining = Math.Max(1, (int)ParseRedisLong(values[2]));
            throw new BadRequestException($"{limitMessage} Please wait {remaining} seconds.");
        }

        private static long ParseRedisLong(RedisResult result)
        {
            if (result.IsNull)
            {
                return 0;
            }

            try
            {
                return (long)result;
            }
            catch
            {
                return long.TryParse(result.ToString(), out var value) ? value : 0;
            }
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

        private static EmailVerificationSecurityOptions NormalizeOptions(EmailVerificationSecurityOptions options)
        {
            options.ResendCooldownSeconds = options.ResendCooldownSeconds <= 0 ? 60 : options.ResendCooldownSeconds;
            options.MaxSendsPerWindow = options.MaxSendsPerWindow <= 0 ? 3 : options.MaxSendsPerWindow;
            options.SendWindowMinutes = options.SendWindowMinutes <= 0 ? 15 : options.SendWindowMinutes;
            options.MaxSendsPerDay = options.MaxSendsPerDay <= 0 ? 10 : options.MaxSendsPerDay;
            options.MaxSendsPerIpWindow = options.MaxSendsPerIpWindow <= 0 ? 10 : options.MaxSendsPerIpWindow;
            options.IpSendWindowMinutes = options.IpSendWindowMinutes <= 0 ? 15 : options.IpSendWindowMinutes;
            options.MaxSendsPerIpDay = options.MaxSendsPerIpDay <= 0 ? 200 : options.MaxSendsPerIpDay;
            options.MaxSendsPerEmailIpWindow = options.MaxSendsPerEmailIpWindow < 0 ? 5 : options.MaxSendsPerEmailIpWindow;
            options.EmailIpSendWindowMinutes = options.EmailIpSendWindowMinutes <= 0 ? 15 : options.EmailIpSendWindowMinutes;
            options.MaxGlobalSendsPerWindow = options.MaxGlobalSendsPerWindow <= 0 ? 1000 : options.MaxGlobalSendsPerWindow;
            options.GlobalSendWindowMinutes = options.GlobalSendWindowMinutes <= 0 ? 60 : options.GlobalSendWindowMinutes;
            return options;
        }
    }
}
