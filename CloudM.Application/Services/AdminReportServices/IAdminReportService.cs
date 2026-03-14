using CloudM.Application.DTOs.AdminDTOs;

namespace CloudM.Application.Services.AdminReportServices
{
    public interface IAdminReportService
    {
        Task<AdminReportItemResponse> CreateInternalReportAsync(
            Guid adminId,
            AdminReportCreateRequest request,
            string? requesterIpAddress);
        Task<AdminReportListResponse> GetRecentReportsAsync(AdminReportListRequest request);
        Task<AdminReportItemResponse> UpdateStatusAsync(
            Guid adminId,
            Guid moderationReportId,
            AdminReportStatusUpdateRequest request,
            string? requesterIpAddress);
    }
}
