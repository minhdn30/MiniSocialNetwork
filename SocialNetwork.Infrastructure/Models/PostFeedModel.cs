using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class PostFeedModel
    {
        public Guid PostId { get; set; }
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

    }
}
