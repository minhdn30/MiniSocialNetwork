using System.ComponentModel.DataAnnotations;

namespace SocialNetwork.Domain.Entities
{
    public class StoryHighlightGroup
    {
        public Guid StoryHighlightGroupId { get; set; }
        public Guid AccountId { get; set; }

        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? CoverImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual Account Account { get; set; } = null!;
        public virtual ICollection<StoryHighlightItem> Items { get; set; } = new List<StoryHighlightItem>();
    }
}
