using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Data;

namespace CloudM.Infrastructure.Repositories.AdminAccountStatuses
{
    public class AdminAccountStatusRepository : IAdminAccountStatusRepository
    {
        private readonly AppDbContext _context;

        public AdminAccountStatusRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Account?> GetTrackedAccountByIdAsync(Guid accountId)
        {
            return await _context.Accounts
                .Include(account => account.Role)
                .FirstOrDefaultAsync(account => account.AccountId == accountId);
        }

        public Task UpdateAsync(Account account)
        {
            _context.Accounts.Update(account);
            return Task.CompletedTask;
        }
    }
}
