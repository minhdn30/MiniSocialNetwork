namespace CloudM.Infrastructure.Models
{
    public class BlockedAccountListItemModel
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime BlockedAt { get; set; }
    }
}
