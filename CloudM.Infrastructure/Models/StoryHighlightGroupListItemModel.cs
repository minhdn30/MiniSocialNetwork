namespace CloudM.Infrastructure.Models
{
    public class StoryHighlightGroupListItemModel
    {
        public Guid StoryHighlightGroupId { get; set; }
        public Guid AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int StoryCount { get; set; }
        public StoryHighlightArchiveCandidateModel? FallbackStory { get; set; }
    }
}
