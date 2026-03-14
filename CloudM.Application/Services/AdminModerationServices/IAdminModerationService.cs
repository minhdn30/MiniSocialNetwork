using CloudM.Application.DTOs.AdminDTOs;

namespace CloudM.Application.Services.AdminModerationServices
{
    public interface IAdminModerationService
    {
        Task<AdminModerationLookupResponse> LookupAsync(AdminModerationLookupRequest request);
        Task<AdminModerationActionResponse> ApplyActionAsync(
            Guid adminId,
            Guid targetId,
            AdminModerationActionRequest request,
            string targetType,
            string? requesterIpAddress);
    }
}
