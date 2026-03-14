using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AdminAccountLookups
{
    public interface IAdminAccountLookupRepository
    {
        Task<List<AdminAccountLookupItemModel>> LookupAccountsAsync(string keyword, int limit);
    }
}
