using CloudM.Application.DTOs.ReportDTOs;

namespace CloudM.Application.Services.ReportServices
{
    public interface IReportService
    {
        Task<ReportCreateResponse> CreateReportAsync(Guid currentId, ReportCreateRequest request, string? requesterIpAddress);
    }
}
