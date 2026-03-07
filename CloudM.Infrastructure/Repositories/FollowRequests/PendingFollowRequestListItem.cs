namespace CloudM.Infrastructure.Repositories.FollowRequests
{
    public class PendingFollowRequestListItem
    {
        public Guid RequesterId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
