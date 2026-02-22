using SocialNetwork.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SocialNetwork.Domain.Entities
{
    public class Story
    {
        public Guid StoryId { get; set; }
        public Guid AccountId { get; set; }
        public StoryContentTypeEnum ContentType { get; set; }
        public string? MediaUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        [MaxLength(1000)]
        public string? TextContent { get; set; }
        public StoryPrivacyEnum Privacy { get; set; } = StoryPrivacyEnum.Public;
        public DateTime ExpiresAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Account Account { get; set; } = null!;
        public virtual ICollection<StoryView> Views { get; set; } = new List<StoryView>();
    }
}
