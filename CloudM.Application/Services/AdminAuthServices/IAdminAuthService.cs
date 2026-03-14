using CloudM.Application.DTOs.AdminDTOs;

namespace CloudM.Application.Services.AdminAuthServices
{
    public interface IAdminAuthService
    {
        Task<AdminLoginResponse> LoginAsync(AdminLoginRequest request, string? requesterIpAddress);
        Task<AdminSessionResponse> GetSessionAsync(Guid accountId);
        Task<AdminChangePasswordResponse> ChangePasswordAsync(
            Guid accountId,
            AdminChangePasswordRequest request,
            string? requesterIpAddress);
    }
}
