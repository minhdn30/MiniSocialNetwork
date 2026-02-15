using SocialNetwork.Application.DTOs.PostMediaDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class PostDetailModel
    {
        public Guid PostId { get; set; }
        public string PostCode { get; set; } = string.Empty;
        public AccountBasicInfoModel Owner { get; set; } = null!;
        public int Privacy { get; set; }
        public int FeedAspectRatio { get; set; }
        public string? Content { get; set; }
        public List<PostMediaProfilePreviewModel> Medias { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsReactedByCurrentUser { get; set; }
        public bool IsOwner { get; set; }
        public int TotalMedias { get; set; } = 0;
        public int TotalReacts { get; set; } = 0;
        public int TotalComments { get; set; } = 0;
        public bool IsFollowedByCurrentUser { get; set; }
    }
}
