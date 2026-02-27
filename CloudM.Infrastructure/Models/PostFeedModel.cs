using CloudM.Domain.Enums;
using System.Text.Json.Serialization;

namespace CloudM.Infrastructure.Models
{
    public class PostFeedModel
    {
        public Guid PostId { get; set; }
        public string PostCode { get; set; } = string.Empty;
        public AccountOnFeedModel Author { get; set; } = null!;
        public string? Content { get; set; }
        public PostPrivacyEnum Privacy { get; set; }
        public AspectRatioEnum FeedAspectRatio { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<MediaPostPersonalListModel>? Medias { get; set; } = new();
        public int MediaCount { get; set; }
        public int ReactCount { get; set; }
        public int CommentCount { get; set; }
        public bool IsReactedByCurrentUser { get; set; }
        public bool IsOwner { get; set; }
        [JsonIgnore]
        public int ReplyCount { get; set; }
        [JsonIgnore]
        public bool IsFollowedAuthor { get; set; }

    }
}
