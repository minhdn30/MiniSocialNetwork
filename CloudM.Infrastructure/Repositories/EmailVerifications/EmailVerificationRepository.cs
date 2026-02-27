using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.EmailVerifications
{
    public class EmailVerificationRepository : IEmailVerificationRepository
    {
        private readonly AppDbContext _context;
        public EmailVerificationRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<EmailVerification?> GetLatestByEmailAsync(string email)
        {
            return await _context.EmailVerifications
                .Where(e => e.Email == email)
                .OrderByDescending(e => e.CreatedAt)
                .ThenByDescending(e => e.Id)
                .FirstOrDefaultAsync();
        }

        public async Task EnsureExistsByEmailAsync(string email, DateTime nowUtc)
        {
            var dailyWindowStart = nowUtc.Date;
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""EmailVerifications""
                (""Email"", ""CodeHash"", ""CodeSalt"", ""FailedAttempts"", ""LastSentAt"", ""SendCountInWindow"", ""SendWindowStartedAt"", ""DailySendCount"", ""DailyWindowStartedAt"", ""LockedUntil"", ""ConsumedAt"", ""ExpiredAt"", ""CreatedAt"")
                VALUES
                ({email}, {string.Empty}, {string.Empty}, 0, {DateTime.UnixEpoch}, 0, {nowUtc}, 0, {dailyWindowStart}, {null}, {null}, {nowUtc}, {nowUtc})
                ON CONFLICT (""Email"") DO NOTHING;");
        }

        public async Task AddEmailVerificationAsync(EmailVerification emailVerification)
        {
            await _context.EmailVerifications.AddAsync(emailVerification);
        }
        public Task UpdateEmailVerificationAsync(EmailVerification emailVerification)
        {
            _context.EmailVerifications.Update(emailVerification);
            return Task.CompletedTask;
        }

        public async Task DeleteEmailVerificationAsync(string email)
        {
            await _context.EmailVerifications
                .Where(e => e.Email == email)
                .ExecuteDeleteAsync();
        }

        public async Task<bool> TryMarkAsConsumedAsync(int verificationId, DateTime consumedAtUtc)
        {
            var affectedRows = await _context.EmailVerifications
                .Where(e => e.Id == verificationId && e.ConsumedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.ConsumedAt, consumedAtUtc));

            return affectedRows > 0;
        }

        public async Task<int> CleanupStaleVerificationsAsync(DateTime nowUtc, DateTime createdBeforeUtc)
        {
            return await _context.EmailVerifications
                .Where(e =>
                    e.ExpiredAt <= nowUtc ||
                    (e.LockedUntil != null && e.LockedUntil <= nowUtc.AddMinutes(-5)) ||
                    e.CreatedAt <= createdBeforeUtc ||
                    e.ConsumedAt != null)
                .ExecuteDeleteAsync();
        }
    }
}
