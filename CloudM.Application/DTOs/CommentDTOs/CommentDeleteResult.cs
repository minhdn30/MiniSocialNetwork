using System;

namespace CloudM.Application.DTOs.CommentDTOs
{
    public class CommentDeleteResult
    {
        public Guid PostId { get; set; }
        public Guid? ParentCommentId { get; set; }
        public int? TotalPostComments { get; set; }
        public int? ParentReplyCount { get; set; }
    }
}
