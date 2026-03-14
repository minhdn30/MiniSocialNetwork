using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AdminReports
{
    public interface IAdminReportRepository
    {
        Task AddAsync(ModerationReport report, ModerationReportAction action);
        Task<ModerationReport?> GetTrackedByIdAsync(Guid moderationReportId);
        Task AddActionAsync(ModerationReportAction action);
        Task<List<AdminReportListItemModel>> GetRecentAsync(
            ModerationReportStatusEnum? status,
            ModerationTargetTypeEnum? targetType,
            int limit);
    }
}
