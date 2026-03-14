using CloudM.Application.DTOs.AdminDTOs;

namespace CloudM.Application.Services.AdminAuditLogServices
{
    public interface IAdminAuditLogService
    {
        Task RecordLoginAsync(Guid adminId, string? requesterIpAddress);
        Task RecordAsync(AdminAuditLogWriteRequest request);
        Task<AdminAuditLogResponse> GetRecentLogsAsync(AdminAuditLogQueryRequest request);
    }
}
