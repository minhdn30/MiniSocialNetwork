using CloudM.Domain.Enums;

namespace CloudM.Domain.Entities
{
    public class NotificationContribution
    {
        public Guid ContributionId { get; set; }
        public Guid NotificationId { get; set; }
        public NotificationSourceTypeEnum SourceType { get; set; }
        public Guid SourceId { get; set; }
        public Guid ActorId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual Notification Notification { get; set; } = null!;
        public virtual Account Actor { get; set; } = null!;
    }
}
