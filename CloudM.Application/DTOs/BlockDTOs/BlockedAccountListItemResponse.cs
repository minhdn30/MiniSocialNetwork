namespace CloudM.Application.DTOs.BlockDTOs
{
    public class BlockedAccountListItemResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime BlockedAt { get; set; }
    }
}
