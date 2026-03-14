using CloudM.Domain.Entities;
using CloudM.Domain.Enums;

namespace CloudM.Infrastructure.Repositories.Reports
{
    public interface IReportRepository
    {
        Task<bool> CanSubmitReportAsync(Guid currentId, ModerationTargetTypeEnum targetType, Guid targetId);
        Task<bool> HasPendingDuplicateAsync(Guid currentId, ModerationTargetTypeEnum targetType, Guid targetId);
        Task AddAsync(ModerationReport report);
    }
}
