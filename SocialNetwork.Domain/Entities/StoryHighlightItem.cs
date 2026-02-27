namespace SocialNetwork.Domain.Entities
{
    public class StoryHighlightItem
    {
        public Guid StoryHighlightGroupId { get; set; }
        public Guid StoryId { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public virtual StoryHighlightGroup StoryHighlightGroup { get; set; } = null!;
        public virtual Story Story { get; set; } = null!;
    }
}
