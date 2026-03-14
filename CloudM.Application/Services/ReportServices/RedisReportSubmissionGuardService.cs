using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Net;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.ReportServices
{
    public class RedisReportSubmissionGuardService : IReportSubmissionGuardService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly MemoryReportSubmissionGuardService _memoryFallbackService;
        private readonly ReportSecurityOptions _options;
        private readonly string _keyPrefix;

        public RedisReportSubmissionGuardService(
            IConnectionMultiplexer redis,
            MemoryReportSubmissionGuardService memoryFallbackService,
            IOptions<ReportSecurityOptions> options,
            IConfiguration configuration)
        {
            _redis = redis;
            _memoryFallbackService = memoryFallbackService;
            _options = NormalizeOptions(options.Value);
            _keyPrefix = (configuration["Redis:KeyPrefix"] ?? "cloudm").Trim();
        }

        public async Task EnforceSubmissionAllowedAsync(Guid accountId, string? ipAddress, DateTime nowUtc)
        {
            var normalizedIp = NormalizeIp(ipAddress);
            var database = _redis.GetDatabase();

            try
            {
                await EnsureWithinLimitAsync(
                    database,
                    BuildCurrentWindowKey(
                        BuildAccountCounterPrefix(accountId),
                        _options.WindowMinutes,
                        nowUtc),
                    _options.MaxReportsPerAccountWindow,
                    "You are sending reports too quickly.");

                if (string.IsNullOrWhiteSpace(normalizedIp))
                {
                    return;
                }

                await EnsureWithinLimitAsync(
                    database,
                    BuildCurrentWindowKey(
                        BuildIpCounterPrefix(normalizedIp),
                        _options.WindowMinutes,
                        nowUtc),
                    _options.MaxReportsPerIpWindow,
                    "This network is sending reports too quickly.");
            }
            catch (RedisException)
            {
                await _memoryFallbackService.EnforceSubmissionAllowedAsync(accountId, normalizedIp, nowUtc);
            }
        }

        public async Task RecordSubmissionAsync(Guid accountId, string? ipAddress, DateTime nowUtc)
        {
            var normalizedIp = NormalizeIp(ipAddress);
            var database = _redis.GetDatabase();

            try
            {
                await IncrementFixedWindowCounterAsync(
                    database,
                    BuildCurrentWindowKey(
                        BuildAccountCounterPrefix(accountId),
                        _options.WindowMinutes,
                        nowUtc),
                    _options.WindowMinutes,
                    nowUtc);

                if (!string.IsNullOrWhiteSpace(normalizedIp))
                {
                    await IncrementFixedWindowCounterAsync(
                        database,
                        BuildCurrentWindowKey(
                            BuildIpCounterPrefix(normalizedIp),
                            _options.WindowMinutes,
                            nowUtc),
                        _options.WindowMinutes,
                        nowUtc);
                }

                await _memoryFallbackService.RecordSubmissionAsync(accountId, normalizedIp, nowUtc);
            }
            catch (RedisException)
            {
                await _memoryFallbackService.RecordSubmissionAsync(accountId, normalizedIp, nowUtc);
            }
        }

        private static async Task EnsureWithinLimitAsync(
            IDatabase database,
            string key,
            int maxAllowed,
            string message)
        {
            if (maxAllowed <= 0)
            {
                return;
            }

            var countValue = await database.StringGetAsync(key);
            if (!countValue.HasValue || !long.TryParse(countValue.ToString(), out var count) || count < maxAllowed)
            {
                return;
            }

            var ttl = await database.KeyTimeToLiveAsync(key);
            var remainingSeconds = ttl.HasValue && ttl.Value > TimeSpan.Zero
                ? Math.Max(1, (int)Math.Ceiling(ttl.Value.TotalSeconds))
                : 60;

            throw new TooManyRequestsException($"{message} Please wait {remainingSeconds} seconds and try again.");
        }

        private static async Task IncrementFixedWindowCounterAsync(
            IDatabase database,
            string key,
            int windowMinutes,
            DateTime nowUtc)
        {
            var count = await database.StringIncrementAsync(key);
            if (count != 1)
            {
                return;
            }

            var ttl = CalculateWindowTtl(windowMinutes, nowUtc);
            await database.KeyExpireAsync(key, ttl);
        }

        private static TimeSpan CalculateWindowTtl(int windowMinutes, DateTime nowUtc)
        {
            var normalizedNowUtc = NormalizeUtc(nowUtc);
            var windowSeconds = Math.Max(60, windowMinutes * 60);
            var nowUnix = new DateTimeOffset(normalizedNowUtc).ToUnixTimeSeconds();
            var bucket = nowUnix / windowSeconds;
            var ttlSeconds = Math.Max(1, (bucket + 1) * windowSeconds - nowUnix + 30);

            return TimeSpan.FromSeconds(ttlSeconds);
        }

        private static string BuildCurrentWindowKey(string keyPrefix, int windowMinutes, DateTime nowUtc)
        {
            var normalizedNowUtc = NormalizeUtc(nowUtc);
            var windowSeconds = Math.Max(60, windowMinutes * 60);
            var nowUnix = new DateTimeOffset(normalizedNowUtc).ToUnixTimeSeconds();
            var bucket = nowUnix / windowSeconds;

            return $"{keyPrefix}:w:{bucket}";
        }

        private string BuildAccountCounterPrefix(Guid accountId)
        {
            return $"{_keyPrefix}:report:submit:account:{accountId:D}";
        }

        private string BuildIpCounterPrefix(string ipAddress)
        {
            return $"{_keyPrefix}:report:submit:ip:{ipAddress}";
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

        private static DateTime NormalizeUtc(DateTime nowUtc)
        {
            if (nowUtc.Kind == DateTimeKind.Utc)
            {
                return nowUtc;
            }

            return nowUtc.ToUniversalTime();
        }

        private static ReportSecurityOptions NormalizeOptions(ReportSecurityOptions options)
        {
            options.MaxReportsPerAccountWindow = options.MaxReportsPerAccountWindow <= 0
                ? 8
                : options.MaxReportsPerAccountWindow;
            options.MaxReportsPerIpWindow = options.MaxReportsPerIpWindow <= 0
                ? 24
                : options.MaxReportsPerIpWindow;
            options.WindowMinutes = options.WindowMinutes <= 0
                ? 10
                : options.WindowMinutes;

            return options;
        }
    }
}
