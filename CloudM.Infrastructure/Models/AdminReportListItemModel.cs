using CloudM.Domain.Enums;

namespace CloudM.Infrastructure.Models
{
    public class AdminReportListItemModel
    {
        public Guid ModerationReportId { get; set; }
        public ModerationTargetTypeEnum TargetType { get; set; }
        public Guid TargetId { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string? Detail { get; set; }
        public ModerationReportStatusEnum Status { get; set; }
        public ModerationReportSourceEnum SourceType { get; set; }
        public string ReporterEmail { get; set; } = string.Empty;
        public string ReporterFullname { get; set; } = string.Empty;
        public string CreatedByAdminEmail { get; set; } = string.Empty;
        public string CreatedByAdminFullname { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
