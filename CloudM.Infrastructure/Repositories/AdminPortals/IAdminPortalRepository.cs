using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AdminPortals
{
    public interface IAdminPortalRepository
    {
        Task<AdminPortalBootstrapModel> GetBootstrapAsync();
    }
}
