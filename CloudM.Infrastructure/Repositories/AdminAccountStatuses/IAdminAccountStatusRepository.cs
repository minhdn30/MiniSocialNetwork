using CloudM.Domain.Entities;

namespace CloudM.Infrastructure.Repositories.AdminAccountStatuses
{
    public interface IAdminAccountStatusRepository
    {
        Task<Account?> GetTrackedAccountByIdAsync(Guid accountId);
        Task UpdateAsync(Account account);
    }
}
