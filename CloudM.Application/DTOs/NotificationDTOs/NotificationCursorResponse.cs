namespace CloudM.Application.DTOs.NotificationDTOs
{
    public class NotificationCursorResponse
    {
        public List<NotificationItemResponse> Items { get; set; } = new();
        public NotificationNextCursorResponse? NextCursor { get; set; }
    }

    public class NotificationNextCursorResponse
    {
        public DateTime LastEventAt { get; set; }
        public Guid NotificationId { get; set; }
    }
}
