namespace SocialNetwork.Application.DTOs.StoryHighlightDTOs
{
    public class StoryHighlightGroupStoriesResponse
    {
        public Guid StoryHighlightGroupId { get; set; }
        public Guid AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<StoryHighlightStoryItemResponse> Stories { get; set; } = new();
        public int StoryCount => Stories.Count;
    }
}
