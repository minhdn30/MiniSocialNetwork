using Microsoft.EntityFrameworkCore;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AdminAccountLookups
{
    public class AdminAccountLookupRepository : IAdminAccountLookupRepository
    {
        private readonly AppDbContext _context;

        public AdminAccountLookupRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<AdminAccountLookupItemModel>> LookupAccountsAsync(string keyword, int limit)
        {
            var normalizedKeyword = (keyword ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                return new List<AdminAccountLookupItemModel>();
            }

            var safeLimit = Math.Clamp(limit, 1, 20);
            var normalizedLowerKeyword = normalizedKeyword.ToLowerInvariant();
            var hasAccountIdMatch = Guid.TryParse(normalizedKeyword, out var accountIdKeyword);
            var isEmailKeyword = normalizedKeyword.Contains('@');

            var query = _context.Accounts
                .AsNoTracking()
                .Select(account => new
                {
                    account.AccountId,
                    account.Username,
                    account.FullName,
                    account.Email,
                    account.AvatarUrl,
                    RoleName = account.Role.RoleName,
                    account.Status,
                    account.CreatedAt,
                    account.LastOnlineAt,
                    AccountIdExact = hasAccountIdMatch && account.AccountId == accountIdKeyword,
                    EmailExact = account.Email == normalizedLowerKeyword,
                    UsernameExact = account.Username == normalizedLowerKeyword,
                    EmailStartsWith = account.Email.StartsWith(normalizedLowerKeyword),
                    UsernameStartsWith = account.Username.StartsWith(normalizedLowerKeyword),
                });

            if (hasAccountIdMatch)
            {
                return await query
                    .Where(item => item.AccountIdExact)
                    .Take(1)
                    .Select(item => new AdminAccountLookupItemModel
                    {
                        AccountId = item.AccountId,
                        Username = item.Username,
                        FullName = item.FullName,
                        Email = item.Email,
                        AvatarUrl = item.AvatarUrl,
                        RoleName = item.RoleName,
                        Status = item.Status,
                        CreatedAt = item.CreatedAt,
                        LastOnlineAt = item.LastOnlineAt,
                    })
                    .ToListAsync();
            }

            var filteredQuery = isEmailKeyword
                ? query.Where(item => item.EmailExact || item.EmailStartsWith)
                    .OrderByDescending(item => item.EmailExact)
                    .ThenByDescending(item => item.EmailStartsWith)
                : query.Where(item => item.UsernameExact || item.UsernameStartsWith)
                    .OrderByDescending(item => item.UsernameExact)
                    .ThenByDescending(item => item.UsernameStartsWith);

            return await filteredQuery
                .ThenByDescending(item => item.LastOnlineAt)
                .ThenBy(item => item.Username)
                .Take(safeLimit)
                .Select(item => new AdminAccountLookupItemModel
                {
                    AccountId = item.AccountId,
                    Username = item.Username,
                    FullName = item.FullName,
                    Email = item.Email,
                    AvatarUrl = item.AvatarUrl,
                    RoleName = item.RoleName,
                    Status = item.Status,
                    CreatedAt = item.CreatedAt,
                    LastOnlineAt = item.LastOnlineAt,
                })
                .ToListAsync();
        }
    }
}
