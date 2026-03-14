using CloudM.Application.DTOs.AdminDTOs;

namespace CloudM.Application.Services.AdminAccountLookupServices
{
    public interface IAdminAccountLookupService
    {
        Task<AdminAccountLookupResponse> LookupAccountsAsync(AdminAccountLookupRequest request);
    }
}
