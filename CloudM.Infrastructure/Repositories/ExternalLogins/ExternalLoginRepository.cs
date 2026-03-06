using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;

namespace CloudM.Infrastructure.Repositories.ExternalLogins
{
    public class ExternalLoginRepository : IExternalLoginRepository
    {
        private readonly AppDbContext _context;

        public ExternalLoginRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ExternalLogin?> GetByProviderUserIdAsync(ExternalLoginProviderEnum provider, string providerUserId)
        {
            var normalizedProviderUserId = (providerUserId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedProviderUserId))
            {
                return null;
            }

            return await _context.ExternalLogins
                .Include(x => x.Account)
                    .ThenInclude(a => a.Role)
                .Include(x => x.Account)
                    .ThenInclude(a => a.Settings)
                .FirstOrDefaultAsync(x =>
                    x.Provider == provider &&
                    x.ProviderUserId == normalizedProviderUserId);
        }

        public async Task<ExternalLogin?> GetByAccountIdAndProviderAsync(Guid accountId, ExternalLoginProviderEnum provider)
        {
            return await _context.ExternalLogins
                .FirstOrDefaultAsync(x => x.AccountId == accountId && x.Provider == provider);
        }

        public async Task<List<ExternalLogin>> GetByAccountIdAsync(Guid accountId)
        {
            return await _context.ExternalLogins
                .Where(x => x.AccountId == accountId)
                .OrderBy(x => x.Provider)
                .ToListAsync();
        }

        public Task<int> CountByAccountIdAsync(Guid accountId)
        {
            return _context.ExternalLogins.CountAsync(x => x.AccountId == accountId);
        }

        public async Task AddAsync(ExternalLogin externalLogin)
        {
            await _context.ExternalLogins.AddAsync(externalLogin);
        }

        public Task DeleteAsync(ExternalLogin externalLogin)
        {
            _context.ExternalLogins.Remove(externalLogin);
            return Task.CompletedTask;
        }
    }
}
