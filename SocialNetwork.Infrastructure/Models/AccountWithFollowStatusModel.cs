using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class AccountWithFollowStatusModel : AccountBasicInfoModel
    {
        public bool IsFollowing { get; set; } // CurrentUser follows this account
        public bool IsFollower { get; set; }  // This account follows CurrentUser
    }
}
