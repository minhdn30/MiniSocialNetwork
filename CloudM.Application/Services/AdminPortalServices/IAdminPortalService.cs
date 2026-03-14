using CloudM.Application.DTOs.AdminDTOs;

namespace CloudM.Application.Services.AdminPortalServices
{
    public interface IAdminPortalService
    {
        Task<AdminPortalBootstrapResponse> GetBootstrapAsync();
    }
}
