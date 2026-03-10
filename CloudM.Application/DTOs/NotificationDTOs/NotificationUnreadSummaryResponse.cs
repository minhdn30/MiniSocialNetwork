namespace CloudM.Application.DTOs.NotificationDTOs
{
    public class NotificationUnreadSummaryResponse
    {
        public Guid AccountId { get; set; }
        public int Count { get; set; }
        public int NotificationUnreadCount { get; set; }
        public int FollowRequestUnreadCount { get; set; }
        public int PendingFollowRequestCount { get; set; }
        public DateTime? LastNotificationsSeenAt { get; set; }
        public DateTime? LastFollowRequestsSeenAt { get; set; }
    }
}
