using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class ReplyCommentModel
    {
        public Guid CommentId { get; set; }
        public Guid PostId { get; set; }
        public Guid ParentCommentId { get; set; }
        public AccountBasicInfoModel Owner { get; set; } = null!;
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ReactCount { get; set; }
        public bool IsCommentReactedByCurrentUser { get; set; } = false;
    }
}
