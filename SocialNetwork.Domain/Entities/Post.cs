using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SocialNetwork.Domain.Entities
{
    public class Post
    {
        public Guid PostId { get; set; }
        public Guid AccountId { get; set; }
        [Required]
        [StringLength(12)]
        public string PostCode { get; set; } = string.Empty;
        public string? Content { get; set; }
        public PostPrivacyEnum Privacy { get; set; } = PostPrivacyEnum.Public; // 0=Public, 1=FollowOnly, 2=Private
        public AspectRatioEnum FeedAspectRatio { get; set; } = AspectRatioEnum.Original; // 0=Original, 1=Square, 2=Portrait, 3=Landscape
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public virtual Account Account { get; set; } = null!;
        public virtual ICollection<PostMedia> Medias { get; set; } = new List<PostMedia>();
        public virtual ICollection<PostReact> Reacts { get; set; } = new List<PostReact>();
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }

}
