namespace CloudM.Application.DTOs.NotificationDTOs
{
    public class NotificationCursorRequest
    {
        public int Limit { get; set; } = 20;
        public DateTime? CursorLastEventAt { get; set; }
        public Guid? CursorNotificationId { get; set; }
        public string Filter { get; set; } = "all";
    }
}
