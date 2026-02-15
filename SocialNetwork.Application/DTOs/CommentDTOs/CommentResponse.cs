using SocialNetwork.Application.DTOs.AccountDTOs;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.CommentDTOs
{
    public class CommentResponse
    {
        public Guid CommentId { get; set; }
        public Guid PostId { get; set; }
        public AccountBasicInfoResponse Owner { get; set; } = null!;
        public string Content { get; set; } = null!;
        public Guid? ParentCommentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ReactCount { get; set; } = 0;
        public int ReplyCount { get; set; } = 0;
        public int TotalCommentCount { get; set; } = 0;
        public bool IsCommentReactedByCurrentUser { get; set; } = false;
        public bool CanDelete { get; set; } = false;
        public bool CanEdit { get; set; } = false;
    }
}
