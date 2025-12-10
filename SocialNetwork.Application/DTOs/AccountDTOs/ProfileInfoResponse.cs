using SocialNetwork.Application.DTOs.FollowDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.AccountDTOs
{
    public class ProfileInfoResponse
    {
        public ProfileDetailResponse AccountInfo { get; set; }
        public FollowCountResponse FollowInfo { get; set; }
        public int TotalPosts { get; set; }
    }
}
