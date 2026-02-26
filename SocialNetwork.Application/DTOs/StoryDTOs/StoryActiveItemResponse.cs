namespace SocialNetwork.Application.DTOs.StoryDTOs
{
    public class StoryActiveItemResponse
    {
        public Guid StoryId { get; set; }
        public int ContentType { get; set; }
        public string? MediaUrl { get; set; }
        public string? TextContent { get; set; }
        public string? BackgroundColorKey { get; set; }
        public string? FontTextKey { get; set; }
        public string? FontSizeKey { get; set; }
        public string? TextColorKey { get; set; }
        public int Privacy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsViewedByCurrentUser { get; set; }
        public int? CurrentUserReactType { get; set; }
        public StoryViewSummaryResponse? ViewSummary { get; set; }
    }
}
