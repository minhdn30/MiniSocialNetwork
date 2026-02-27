namespace CloudM.Application.DTOs.StoryDTOs
{
    public class StoryViewerBasicResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public DateTime ViewedAt { get; set; }
        public int? ReactType { get; set; }
    }
}
