namespace CloudM.Infrastructure.Models
{
    public class SidebarAccountSearchModel
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public DateTime? LastSearchedAt { get; set; }
    }
}
