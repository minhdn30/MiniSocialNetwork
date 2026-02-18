using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.AccountSettingDTOs
{
    public class AccountSettingsResponse
    {
        public Guid AccountId { get; set; }
        public AccountPrivacyEnum PhonePrivacy { get; set; }
        public AccountPrivacyEnum AddressPrivacy { get; set; }
        public PostPrivacyEnum DefaultPostPrivacy { get; set; }
        public AccountPrivacyEnum FollowerPrivacy { get; set; }
        public AccountPrivacyEnum FollowingPrivacy { get; set; }
        public GroupChatInvitePermissionEnum GroupChatInvitePermission { get; set; }
    }
}
