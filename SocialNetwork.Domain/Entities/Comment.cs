using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Entities
{
    public class Comment
    {
        public Guid CommentId { get; set; }
        public Guid PostId { get; set; }
        public Guid AccountId { get; set; }
        [Required]
        [MaxLength(1000)]
        public string Content { get; set; }
        public Guid? ParentCommentId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public virtual Account Account { get; set; } = null!;
        public virtual Post Post { get; set; }
        public Comment? ParentComment { get; set; }
        public ICollection<Comment> Replies { get; set; } = new List<Comment>();
        public virtual ICollection<CommentReact> CommentReacts { get; set; } = new List<CommentReact>();

    }

}
