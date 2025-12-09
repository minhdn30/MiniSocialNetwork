using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Entities
{
    public class CommentReact
    {
        [Required]
        public Guid CommentId { get; set; }

        [Required]
        public Guid AccountId { get; set; }

        [Required]
        public ReactEnum ReactType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Comment Comment { get; set; } = null!;
        public virtual Account Account { get; set; } = null!;
    }

}
