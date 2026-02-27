using CloudM.Domain.Enums;

namespace CloudM.Infrastructure.Models
{
    public class StoryHighlightStoryItemModel
    {
        public Guid StoryId { get; set; }
        public Guid AccountId { get; set; }
        public StoryContentTypeEnum ContentType { get; set; }
        public string? MediaUrl { get; set; }
        public string? TextContent { get; set; }
        public string? BackgroundColorKey { get; set; }
        public string? FontTextKey { get; set; }
        public string? FontSizeKey { get; set; }
        public string? TextColorKey { get; set; }
        public StoryPrivacyEnum Privacy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsViewedByCurrentUser { get; set; }
        public ReactEnum? CurrentUserReactType { get; set; }
    }
}
