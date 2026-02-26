using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Infrastructure.Models
{
    public class ConversationMediaItemModel
    {
        public Guid MessageId { get; set; }
        public Guid MessageMediaId { get; set; }
        public string MediaUrl { get; set; } = null!;
        public string? ThumbnailUrl { get; set; }
        public MediaTypeEnum MediaType { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
