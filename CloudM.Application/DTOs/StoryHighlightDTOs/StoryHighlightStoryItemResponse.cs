namespace CloudM.Application.DTOs.StoryHighlightDTOs
{
    public class StoryHighlightStoryItemResponse
    {
        public Guid StoryId { get; set; }
        public int ContentType { get; set; }
        public string? MediaUrl { get; set; }
        public string? TextContent { get; set; }
        public string? BackgroundColorKey { get; set; }
        public string? FontTextKey { get; set; }
        public string? FontSizeKey { get; set; }
        public string? TextColorKey { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsViewedByCurrentUser { get; set; }
        public int? CurrentUserReactType { get; set; }
    }
}
