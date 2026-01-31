using SocialNetwork.Application.DTOs.PostMediaDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class AccountProfilePreviewModel
    {
        public AccountBasicInfoModel Account { get; set; } = null!;
        public int PostCount { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public bool IsFollowedByCurrentUser { get; set; } = false;
        public bool IsCurrentUser { get; set; } = false;
        public List<PostMediaProfilePreviewModel>? RecentPosts { get; set; }
    }
}
