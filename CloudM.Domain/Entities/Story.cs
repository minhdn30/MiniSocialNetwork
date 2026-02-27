using CloudM.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace CloudM.Domain.Entities
{
    public class Story
    {
        public Guid StoryId { get; set; }
        public Guid AccountId { get; set; }
        public StoryContentTypeEnum ContentType { get; set; }
        public string? MediaUrl { get; set; }
        [MaxLength(1000)]
        public string? TextContent { get; set; }
        [MaxLength(100)]
        public string? BackgroundColorKey { get; set; }
        [MaxLength(100)]
        public string? FontTextKey { get; set; }
        [MaxLength(100)]
        public string? FontSizeKey { get; set; }
        [MaxLength(100)]
        public string? TextColorKey { get; set; }
        public StoryPrivacyEnum Privacy { get; set; } = StoryPrivacyEnum.Public;
        public DateTime ExpiresAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Account Account { get; set; } = null!;
        public virtual ICollection<StoryView> Views { get; set; } = new List<StoryView>();
        public virtual ICollection<StoryHighlightItem> StoryHighlightItems { get; set; } = new List<StoryHighlightItem>();
    }
}
