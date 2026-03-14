using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudM.Domain.Entities
{
    public class AdminAuditLog
    {
        public Guid AdminAuditLogId { get; set; } = Guid.NewGuid();
        public Guid AdminId { get; set; }

        [Required, MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string Module { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string ActionType { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string? TargetType { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string? TargetId { get; set; }

        [Required, MaxLength(300)]
        [Column(TypeName = "varchar(300)")]
        public string Summary { get; set; } = string.Empty;

        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? RequestIp { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Account Admin { get; set; } = null!;
    }
}
