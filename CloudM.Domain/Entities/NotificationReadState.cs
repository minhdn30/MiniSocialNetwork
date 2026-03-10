namespace CloudM.Domain.Entities
{
    public class NotificationReadState
    {
        public Guid AccountId { get; set; }
        public DateTime? LastNotificationsSeenAt { get; set; }
        public DateTime? LastFollowRequestsSeenAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual Account Account { get; set; } = null!;
    }
}
