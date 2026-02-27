using CloudM.Application.DTOs.FollowDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.AccountDTOs
{
    public class AccountInfoResponse
    {
        public AccountDetailResponse AccountInfo { get; set; } = null!;
        public FollowCountResponse FollowInfo { get; set; } = null!;
        public int TotalPosts { get; set; }
    }
}
