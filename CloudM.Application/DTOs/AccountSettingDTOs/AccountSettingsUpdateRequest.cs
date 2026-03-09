using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace CloudM.Application.DTOs.AccountSettingDTOs
{
    public class AccountSettingsUpdateRequest
    {
        private string? _language;

        public AccountPrivacyEnum? PhonePrivacy { get; set; }
        public AccountPrivacyEnum? AddressPrivacy { get; set; }
        public PostPrivacyEnum? DefaultPostPrivacy { get; set; }
        public AccountPrivacyEnum? FollowerPrivacy { get; set; }
        public AccountPrivacyEnum? FollowingPrivacy { get; set; }
        public FollowPrivacyEnum? FollowPrivacy { get; set; }
        public AccountPrivacyEnum? StoryHighlightPrivacy { get; set; }
        public GroupChatInvitePermissionEnum? GroupChatInvitePermission { get; set; }
        public OnlineStatusVisibilityEnum? OnlineStatusVisibility { get; set; }
        public TagPermissionEnum? TagPermission { get; set; }
        public string? Language
        {
            get => _language;
            set
            {
                HasLanguage = true;
                _language = value;
            }
        }

        [JsonIgnore]
        public bool HasLanguage { get; private set; }
    }
}
