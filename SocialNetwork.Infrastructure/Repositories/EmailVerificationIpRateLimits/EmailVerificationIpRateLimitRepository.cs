using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;

namespace SocialNetwork.Infrastructure.Repositories.EmailVerificationIpRateLimits
{
    public class EmailVerificationIpRateLimitRepository : IEmailVerificationIpRateLimitRepository
    {
        private readonly AppDbContext _context;

        public EmailVerificationIpRateLimitRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<EmailVerificationIpRateLimit?> GetByIpAsync(string ipAddress)
        {
            return await _context.EmailVerificationIpRateLimits
                .FirstOrDefaultAsync(x => x.IpAddress == ipAddress);
        }

        public async Task EnsureExistsByIpAsync(string ipAddress, DateTime nowUtc)
        {
            var dailyWindowStart = nowUtc.Date;
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""EmailVerificationIpRateLimits""
                (""IpAddress"", ""LastSentAt"", ""SendCountInWindow"", ""SendWindowStartedAt"", ""DailySendCount"", ""DailyWindowStartedAt"", ""LockedUntil"", ""CreatedAt"", ""UpdatedAt"")
                VALUES
                ({ipAddress}, {DateTime.UnixEpoch}, 0, {nowUtc}, 0, {dailyWindowStart}, {null}, {nowUtc}, {nowUtc})
                ON CONFLICT (""IpAddress"") DO NOTHING;");
        }

        public async Task AddAsync(EmailVerificationIpRateLimit ipRateLimit)
        {
            await _context.EmailVerificationIpRateLimits.AddAsync(ipRateLimit);
        }

        public Task UpdateAsync(EmailVerificationIpRateLimit ipRateLimit)
        {
            _context.EmailVerificationIpRateLimits.Update(ipRateLimit);
            return Task.CompletedTask;
        }

        public async Task<int> CleanupStaleAsync(DateTime nowUtc, DateTime updatedBeforeUtc)
        {
            return await _context.EmailVerificationIpRateLimits
                .Where(x =>
                    x.UpdatedAt <= updatedBeforeUtc ||
                    (x.LockedUntil != null && x.LockedUntil <= nowUtc.AddMinutes(-5)))
                .ExecuteDeleteAsync();
        }
    }
}
