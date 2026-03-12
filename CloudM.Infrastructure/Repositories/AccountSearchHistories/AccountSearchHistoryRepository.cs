using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Helpers;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AccountSearchHistories
{
    public class AccountSearchHistoryRepository : IAccountSearchHistoryRepository
    {
        private const int DefaultSidebarHistoryLimit = 12;
        private const int MaxSidebarHistoryLimit = 30;

        private readonly AppDbContext _context;

        public AccountSearchHistoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<SidebarAccountSearchModel>> GetSidebarSearchHistoryAsync(
            Guid currentId,
            int limit = 12)
        {
            var safeLimit = NormalizeSidebarHistoryLimit(limit);
            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);

            return await _context.AccountSearchHistories
                .AsNoTracking()
                .Where(x => x.CurrentId == currentId)
                .Where(x => x.TargetId != currentId)
                .Where(x => !hiddenAccountIds.Contains(x.TargetId))
                .Where(x =>
                    x.Target.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(x.Target.RoleId))
                .OrderByDescending(x => x.LastSearchedAt)
                .ThenBy(x => x.Target.Username)
                .Select(x => new SidebarAccountSearchModel
                {
                    AccountId = x.TargetId,
                    Username = x.Target.Username,
                    FullName = x.Target.FullName,
                    AvatarUrl = x.Target.AvatarUrl,
                    LastSearchedAt = x.LastSearchedAt
                })
                .Take(safeLimit)
                .ToListAsync();
        }

        public async Task<bool> CanUseSidebarSearchTargetAsync(Guid currentId, Guid targetId)
        {
            if (targetId == Guid.Empty || targetId == currentId)
            {
                return false;
            }

            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);

            return await GetSocialAccountsNoTrackingQuery()
                .Where(a => a.AccountId == targetId)
                .Where(a => !hiddenAccountIds.Contains(a.AccountId))
                .AnyAsync();
        }

        public async Task UpsertSidebarSearchHistoryAsync(Guid currentId, Guid targetId, DateTime searchedAt)
        {
            if (_context.Database.IsRelational())
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ""AccountSearchHistories"" (
    ""CurrentId"",
    ""TargetId"",
    ""LastSearchedAt"",
    ""CreatedAt"")
VALUES (
    {currentId},
    {targetId},
    {searchedAt},
    {searchedAt})
ON CONFLICT (""CurrentId"", ""TargetId"") DO UPDATE
SET ""LastSearchedAt"" = EXCLUDED.""LastSearchedAt"";");
                return;
            }

            var existing = await _context.AccountSearchHistories
                .FirstOrDefaultAsync(x => x.CurrentId == currentId && x.TargetId == targetId);

            if (existing == null)
            {
                await _context.AccountSearchHistories.AddAsync(new AccountSearchHistory
                {
                    CurrentId = currentId,
                    TargetId = targetId,
                    LastSearchedAt = searchedAt,
                    CreatedAt = searchedAt
                });
                return;
            }

            existing.LastSearchedAt = searchedAt;
        }

        public async Task DeleteSidebarSearchHistoryAsync(Guid currentId, Guid targetId)
        {
            var existing = await _context.AccountSearchHistories
                .FirstOrDefaultAsync(x => x.CurrentId == currentId && x.TargetId == targetId);

            if (existing == null)
            {
                return;
            }

            _context.AccountSearchHistories.Remove(existing);
        }

        private IQueryable<Account> GetSocialAccountsNoTrackingQuery()
        {
            return _context.Accounts
                .AsNoTracking()
                .Where(a =>
                    a.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(a.RoleId));
        }

        private static int NormalizeSidebarHistoryLimit(int limit)
        {
            if (limit <= 0)
            {
                return DefaultSidebarHistoryLimit;
            }

            return Math.Min(limit, MaxSidebarHistoryLimit);
        }
    }
}
