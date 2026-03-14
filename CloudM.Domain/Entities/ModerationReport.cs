using CloudM.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudM.Domain.Entities
{
    public class ModerationReport
    {
        public Guid ModerationReportId { get; set; } = Guid.NewGuid();
        public ModerationTargetTypeEnum TargetType { get; set; }
        public Guid TargetId { get; set; }

        [Required, MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string ReasonCode { get; set; } = string.Empty;

        [MaxLength(1000)]
        [Column(TypeName = "varchar(1000)")]
        public string? Detail { get; set; }

        public ModerationReportStatusEnum Status { get; set; } = ModerationReportStatusEnum.Open;
        public ModerationReportSourceEnum SourceType { get; set; } = ModerationReportSourceEnum.AdminInternal;
        public Guid? CreatedByAdminId { get; set; }
        public Guid? ResolvedByAdminId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public virtual Account? CreatedByAdmin { get; set; }
        public virtual Account? ResolvedByAdmin { get; set; }
        public virtual ICollection<ModerationReportAction> Actions { get; set; } = new List<ModerationReportAction>();
    }
}
