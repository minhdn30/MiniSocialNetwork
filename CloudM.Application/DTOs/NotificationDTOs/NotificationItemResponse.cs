namespace CloudM.Application.DTOs.NotificationDTOs
{
    public class NotificationItemResponse
    {
        public Guid NotificationId { get; set; }
        public int Type { get; set; }
        public int State { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastEventAt { get; set; }
        public int ActorCount { get; set; }
        public int EventCount { get; set; }
        public NotificationActorResponse? Actor { get; set; }
        public string Text { get; set; } = string.Empty;
        public int TargetKind { get; set; }
        public Guid? TargetId { get; set; }
        public string? TargetPostCode { get; set; }
        public string? ThumbnailUrl { get; set; }
        public bool CanOpen { get; set; }
    }
}
