using CloudM.Application.DTOs.FollowDTOs;
using CloudM.Application.DTOs.AccountSettingDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.AccountDTOs
{
    public class ProfileInfoResponse
    {
        public ProfileDetailResponse AccountInfo { get; set; } = null!;
        public FollowCountResponse FollowInfo { get; set; } = null!;
        public int TotalPosts { get; set; }
        public bool IsCurrentUser { get; set; }
        public AccountSettingsResponse? Settings { get; set; }
    }
}
