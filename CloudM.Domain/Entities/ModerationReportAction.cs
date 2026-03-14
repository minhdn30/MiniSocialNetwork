using CloudM.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudM.Domain.Entities
{
    public class ModerationReportAction
    {
        public Guid ModerationReportActionId { get; set; } = Guid.NewGuid();
        public Guid ModerationReportId { get; set; }
        public Guid AdminId { get; set; }
        public ModerationReportActionTypeEnum ActionType { get; set; }
        public ModerationReportStatusEnum? FromStatus { get; set; }
        public ModerationReportStatusEnum? ToStatus { get; set; }

        [MaxLength(1000)]
        [Column(TypeName = "varchar(1000)")]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ModerationReport Report { get; set; } = null!;
        public virtual Account Admin { get; set; } = null!;
    }
}
