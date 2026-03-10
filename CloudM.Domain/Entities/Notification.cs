using CloudM.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace CloudM.Domain.Entities
{
    public class Notification
    {
        public Guid NotificationId { get; set; }
        public Guid RecipientId { get; set; }
        public NotificationTypeEnum Type { get; set; }
        [MaxLength(200)]
        public string AggregateKey { get; set; } = string.Empty;
        public NotificationStateEnum State { get; set; } = NotificationStateEnum.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastEventAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int ActorCount { get; set; } = 0;
        public int EventCount { get; set; } = 0;
        public Guid? LastActorId { get; set; }
        [MaxLength(2000)]
        public string? LastActorSnapshot { get; set; }
        public NotificationTargetKindEnum TargetKind { get; set; } = NotificationTargetKindEnum.None;
        public Guid? TargetId { get; set; }
        [MaxLength(4000)]
        public string? PayloadJson { get; set; }

        public virtual Account Recipient { get; set; } = null!;
        public virtual ICollection<NotificationContribution> Contributions { get; set; } = new List<NotificationContribution>();
    }
}
