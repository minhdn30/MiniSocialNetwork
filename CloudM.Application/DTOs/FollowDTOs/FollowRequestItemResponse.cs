namespace CloudM.Application.DTOs.FollowDTOs
{
    public class FollowRequestItemResponse
    {
        public Guid RequesterId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
