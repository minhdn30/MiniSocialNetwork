using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.PostDTOs
{
    public class PostUpdateContentResponse
    {
        public Guid PostId { get; set; }
        public string? Content { get; set; }
        public PostPrivacyEnum Privacy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
