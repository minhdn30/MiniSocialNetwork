using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.AccountSettingDTOs
{
    public class AccountSettingsUpdateRequest
    {
        public AccountPrivacyEnum? PhonePrivacy { get; set; }
        public AccountPrivacyEnum? AddressPrivacy { get; set; }
        public PostPrivacyEnum? DefaultPostPrivacy { get; set; }
        public AccountPrivacyEnum? FollowerPrivacy { get; set; }
        public AccountPrivacyEnum? FollowingPrivacy { get; set; }
        public AccountPrivacyEnum? StoryHighlightPrivacy { get; set; }
        public GroupChatInvitePermissionEnum? GroupChatInvitePermission { get; set; }
        public OnlineStatusVisibilityEnum? OnlineStatusVisibility { get; set; }
    }
}
