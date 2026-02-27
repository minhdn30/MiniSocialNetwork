using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.PostMediaDTOs;
using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.PostDTOs
{
    public class PostDetailResponse
    {
        public Guid PostId { get; set; }
        public string PostCode { get; set; } = string.Empty;
        public AccountBasicInfoResponse Owner { get; set; } = null!;
        public int Privacy { get; set; }
        public int FeedAspectRatio { get; set; }
        public string? Content { get; set; }
        public List<PostMediaDetailResponse> Medias { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsReactedByCurrentUser { get; set; }
        public bool IsOwner { get; set; } = false;
        public int TotalMedias { get; set; } = 0;
        public int TotalReacts { get; set; } = 0;
        public int TotalComments { get; set; } = 0;

    }
}
