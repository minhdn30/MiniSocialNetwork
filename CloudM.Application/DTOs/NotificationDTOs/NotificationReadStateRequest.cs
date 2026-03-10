namespace CloudM.Application.DTOs.NotificationDTOs
{
    public class NotificationReadStateRequest
    {
        public DateTime? NotificationsSeenAt { get; set; }
        public DateTime? FollowRequestsSeenAt { get; set; }
    }
}
