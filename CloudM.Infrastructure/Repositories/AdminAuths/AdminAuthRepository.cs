using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;

namespace CloudM.Infrastructure.Repositories.AdminAuths
{
    public class AdminAuthRepository : IAdminAuthRepository
    {
        private readonly AppDbContext _context;

        public AdminAuthRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Account?> GetAdminByEmailAsync(string email)
        {
            return await _context.Accounts
                .AsNoTracking()
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x =>
                    x.Email == email &&
                    x.RoleId == (int)RoleEnum.Admin);
        }

        public async Task<Account?> GetAdminByIdAsync(Guid accountId)
        {
            return await _context.Accounts
                .AsNoTracking()
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x =>
                    x.AccountId == accountId &&
                    x.RoleId == (int)RoleEnum.Admin);
        }
    }
}
