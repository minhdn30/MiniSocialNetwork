using CloudM.Domain.Entities;

namespace CloudM.Infrastructure.Repositories.AdminAuths
{
    public interface IAdminAuthRepository
    {
        Task<Account?> GetAdminByEmailAsync(string email);
        Task<Account?> GetAdminByIdAsync(Guid accountId);
        Task<Account?> GetTrackedAdminByIdAsync(Guid accountId);
        Task UpdateAsync(Account account);
    }
}
