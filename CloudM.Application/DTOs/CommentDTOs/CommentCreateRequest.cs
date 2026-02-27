using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.CommentDTOs
{
    public class CommentCreateRequest
    {
        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = null!;
        public Guid? ParentCommentId { get; set; }
    }
}
