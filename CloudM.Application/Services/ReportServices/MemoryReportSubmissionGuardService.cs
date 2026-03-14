using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.ReportServices
{
    public class MemoryReportSubmissionGuardService : IReportSubmissionGuardService, IDisposable
    {
        private const long MaxCacheEntries = 50000;
        private static readonly object[] KeyLockStripes = Enumerable
            .Range(0, 64)
            .Select(_ => new object())
            .ToArray();

        private readonly MemoryCache _memoryCache;
        private readonly ReportSecurityOptions _options;
        private readonly string _keyPrefix;

        private sealed class FixedWindowCounterEntry
        {
            public long Count { get; set; }
            public DateTime ExpiresAtUtc { get; set; }
        }

        public MemoryReportSubmissionGuardService(
            IOptions<ReportSecurityOptions> options,
            IConfiguration configuration)
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = MaxCacheEntries,
                CompactionPercentage = 0.2,
                ExpirationScanFrequency = TimeSpan.FromMinutes(1)
            });
            _options = NormalizeOptions(options.Value);
            _keyPrefix = (configuration["Redis:KeyPrefix"] ?? "cloudm").Trim();
        }

        public Task EnforceSubmissionAllowedAsync(Guid accountId, string? ipAddress, DateTime nowUtc)
        {
            var normalizedIp = NormalizeIp(ipAddress);
            var accountKey = BuildCurrentWindowKey(
                BuildAccountCounterPrefix(accountId),
                _options.WindowMinutes,
                nowUtc);

            EnsureWithinLimit(
                accountKey,
                _options.MaxReportsPerAccountWindow,
                "You are sending reports too quickly.",
                nowUtc);

            if (string.IsNullOrWhiteSpace(normalizedIp))
            {
                return Task.CompletedTask;
            }

            var ipKey = BuildCurrentWindowKey(
                BuildIpCounterPrefix(normalizedIp),
                _options.WindowMinutes,
                nowUtc);

            EnsureWithinLimit(
                ipKey,
                _options.MaxReportsPerIpWindow,
                "This network is sending reports too quickly.",
                nowUtc);

            return Task.CompletedTask;
        }

        public Task RecordSubmissionAsync(Guid accountId, string? ipAddress, DateTime nowUtc)
        {
            var normalizedIp = NormalizeIp(ipAddress);

            IncrementFixedWindowCounter(
                BuildCurrentWindowKey(
                    BuildAccountCounterPrefix(accountId),
                    _options.WindowMinutes,
                    nowUtc),
                _options.WindowMinutes,
                nowUtc);

            if (!string.IsNullOrWhiteSpace(normalizedIp))
            {
                IncrementFixedWindowCounter(
                    BuildCurrentWindowKey(
                        BuildIpCounterPrefix(normalizedIp),
                        _options.WindowMinutes,
                        nowUtc),
                    _options.WindowMinutes,
                    nowUtc);
            }

            return Task.CompletedTask;
        }

        private void EnsureWithinLimit(string key, int maxAllowed, string message, DateTime nowUtc)
        {
            if (maxAllowed <= 0)
            {
                return;
            }

            if (!_memoryCache.TryGetValue<FixedWindowCounterEntry>(key, out var entry) || entry == null)
            {
                return;
            }

            var normalizedNowUtc = NormalizeUtc(nowUtc);
            if (entry.ExpiresAtUtc <= normalizedNowUtc)
            {
                _memoryCache.Remove(key);
                return;
            }

            if (entry.Count < maxAllowed)
            {
                return;
            }

            var remainingSeconds = Math.Max(
                1,
                (int)Math.Ceiling((entry.ExpiresAtUtc - normalizedNowUtc).TotalSeconds));
            throw new TooManyRequestsException($"{message} Please wait {remainingSeconds} seconds and try again.");
        }

        private long IncrementFixedWindowCounter(string key, int windowMinutes, DateTime nowUtc)
        {
            var keyLock = GetKeyLock(key);
            lock (keyLock)
            {
                var normalizedNowUtc = NormalizeUtc(nowUtc);
                var (_, expiresAtUtc, ttl) = CalculateWindowMetadata(windowMinutes, normalizedNowUtc);
                var currentEntry = _memoryCache.TryGetValue<FixedWindowCounterEntry>(key, out var cachedEntry)
                    ? cachedEntry
                    : null;

                if (currentEntry == null || currentEntry.ExpiresAtUtc <= normalizedNowUtc)
                {
                    currentEntry = new FixedWindowCounterEntry
                    {
                        Count = 0,
                        ExpiresAtUtc = expiresAtUtc
                    };
                }

                currentEntry.Count += 1;
                currentEntry.ExpiresAtUtc = expiresAtUtc;

                _memoryCache.Set(
                    key,
                    currentEntry,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = ttl,
                        Size = 1
                    });

                return currentEntry.Count;
            }
        }

        private static object GetKeyLock(string key)
        {
            var hash = (key?.GetHashCode() ?? 0) & int.MaxValue;
            return KeyLockStripes[hash % KeyLockStripes.Length];
        }

        private static (long Bucket, DateTime ExpiresAtUtc, TimeSpan Ttl) CalculateWindowMetadata(int windowMinutes, DateTime nowUtc)
        {
            var windowSeconds = Math.Max(60, windowMinutes * 60);
            var nowUnix = new DateTimeOffset(nowUtc).ToUnixTimeSeconds();
            var bucket = nowUnix / windowSeconds;
            var expiresAtUnix = (bucket + 1) * windowSeconds;
            var ttlSeconds = Math.Max(1, expiresAtUnix - nowUnix + 30);

            return (
                bucket,
                DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix).UtcDateTime,
                TimeSpan.FromSeconds(ttlSeconds));
        }

        private static string BuildCurrentWindowKey(string keyPrefix, int windowMinutes, DateTime nowUtc)
        {
            var normalizedNowUtc = NormalizeUtc(nowUtc);
            var (bucket, _, _) = CalculateWindowMetadata(windowMinutes, normalizedNowUtc);
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

        public void Dispose()
        {
            _memoryCache.Dispose();
        }
    }
}
