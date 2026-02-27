namespace SocialNetwork.Application.DTOs.StoryHighlightDTOs
{
    public class StoryHighlightGroupMutationResponse
    {
        public Guid StoryHighlightGroupId { get; set; }
        public int StoryCount { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
