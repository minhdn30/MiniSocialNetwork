namespace CloudM.Infrastructure.Models
{
    public class SidebarAccountSearchModel
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public bool IsFollowing { get; set; }
        public bool IsFollower { get; set; }
        public bool HasDirectConversation { get; set; }
        public DateTime? LastContactedAt { get; set; }
        public DateTime? LastSearchedAt { get; set; }
    }
}
