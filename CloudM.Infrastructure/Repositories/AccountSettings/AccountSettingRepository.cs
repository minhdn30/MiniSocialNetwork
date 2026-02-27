using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.AccountSettingRepos
{
    public class AccountSettingRepository : IAccountSettingRepository
    {
        private readonly AppDbContext _context;

        public AccountSettingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AccountSettings?> GetGetAccountSettingsByAccountIdAsync(Guid accountId)
        {
            return await _context.AccountSettings.FirstOrDefaultAsync(s => s.AccountId == accountId);
        }

        public Task AddAccountSettingsAsync(AccountSettings settings)
        {
            return _context.AccountSettings.AddAsync(settings).AsTask();
        }

        public Task UpdateAccountSettingsAsync(AccountSettings settings)
        {
            _context.AccountSettings.Update(settings);
            return Task.CompletedTask;
        }
    }
}
