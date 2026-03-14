using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AdminReports
{
    public class AdminReportRepository : IAdminReportRepository
    {
        private readonly AppDbContext _context;

        public AdminReportRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(ModerationReport report, ModerationReportAction action)
        {
            await _context.ModerationReports.AddAsync(report);
            await _context.ModerationReportActions.AddAsync(action);
        }

        public async Task<ModerationReport?> GetTrackedByIdAsync(Guid moderationReportId)
        {
            return await _context.ModerationReports
                .FirstOrDefaultAsync(report => report.ModerationReportId == moderationReportId);
        }

        public async Task AddActionAsync(ModerationReportAction action)
        {
            await _context.ModerationReportActions.AddAsync(action);
        }

        public async Task<List<AdminReportListItemModel>> GetRecentAsync(
            ModerationReportStatusEnum? status,
            ModerationTargetTypeEnum? targetType,
            int limit)
        {
            var safeLimit = Math.Clamp(limit, 1, 30);
            var query = _context.ModerationReports
                .AsNoTracking()
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(report => report.Status == status.Value);
            }

            if (targetType.HasValue)
            {
                query = query.Where(report => report.TargetType == targetType.Value);
            }

            return await query
                .OrderByDescending(report => report.CreatedAt)
                .ThenByDescending(report => report.ModerationReportId)
                .Take(safeLimit)
                .Select(report => new AdminReportListItemModel
                {
                    ModerationReportId = report.ModerationReportId,
                    TargetType = report.TargetType,
                    TargetId = report.TargetId,
                    ReasonCode = report.ReasonCode,
                    Detail = report.Detail,
                    Status = report.Status,
                    SourceType = report.SourceType,
                    CreatedByAdminEmail = report.CreatedByAdmin != null ? report.CreatedByAdmin.Email : string.Empty,
                    CreatedByAdminFullname = report.CreatedByAdmin != null ? report.CreatedByAdmin.FullName : string.Empty,
                    CreatedAt = report.CreatedAt,
                    UpdatedAt = report.UpdatedAt,
                    ResolvedAt = report.ResolvedAt,
                })
                .ToListAsync();
        }
    }
}
