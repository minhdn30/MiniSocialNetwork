namespace CloudM.Application.DTOs.FollowDTOs
{
    public class FollowRequestCursorResponse
    {
        public Guid AccountId { get; set; }
        public List<FollowRequestItemResponse> Items { get; set; } = new();
        public int Count { get; set; }
        public int NotificationUnreadCount { get; set; }
        public int FollowRequestUnreadCount { get; set; }
        public int PendingFollowRequestCount { get; set; }
        public int TotalCount { get; set; }
        public DateTime? LastNotificationsSeenAt { get; set; }
        public DateTime? LastFollowRequestsSeenAt { get; set; }
        public FollowRequestNextCursorResponse? NextCursor { get; set; }
    }

    public class FollowRequestNextCursorResponse
    {
        public DateTime CreatedAt { get; set; }
        public Guid RequesterId { get; set; }
    }
}
