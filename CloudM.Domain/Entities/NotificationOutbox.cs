using CloudM.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace CloudM.Domain.Entities
{
    public class NotificationOutbox
    {
        public Guid OutboxId { get; set; }
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;
        public Guid RecipientId { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public NotificationOutboxStatusEnum Status { get; set; } = NotificationOutboxStatusEnum.Pending;
        public int AttemptCount { get; set; } = 0;
        public DateTime? LockedUntil { get; set; }
        public DateTime NextRetryAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        [MaxLength(2000)]
        public string? LastError { get; set; }

        public virtual Account Recipient { get; set; } = null!;
    }
}
