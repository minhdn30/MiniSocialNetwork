using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.AccountSettingRepos
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

        public async Task AddAccountSettingsAsync(AccountSettings settings)
        {
            await _context.AccountSettings.AddAsync(settings);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAccountSettingsAsync(AccountSettings settings)
        {
            _context.AccountSettings.Update(settings);
            await _context.SaveChangesAsync();
        }
    }
}
