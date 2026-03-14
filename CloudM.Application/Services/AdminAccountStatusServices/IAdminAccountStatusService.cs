using CloudM.Application.DTOs.AdminDTOs;

namespace CloudM.Application.Services.AdminAccountStatusServices
{
    public interface IAdminAccountStatusService
    {
        Task<AdminAccountStatusUpdateResponse> UpdateStatusAsync(
            Guid adminId,
            Guid accountId,
            AdminAccountStatusUpdateRequest request,
            string? requesterIpAddress);
    }
}
