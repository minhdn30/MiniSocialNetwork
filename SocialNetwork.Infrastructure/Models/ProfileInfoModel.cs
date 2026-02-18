using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class ProfileInfoModel
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
        public string? Bio { get; set; }
        public string? CoverUrl { get; set; }
        public bool? Gender { get; set; }
        public string? Address { get; set; }
        public DateTime CreatedAt { get; set; }
        public int PostCount { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public bool IsFollowedByCurrentUser { get; set; }
        public bool IsCurrentUser { get; set; }

        // Settings (Projected for optimization)
        public AccountPrivacyEnum PhonePrivacy { get; set; }
        public AccountPrivacyEnum AddressPrivacy { get; set; }
        public PostPrivacyEnum DefaultPostPrivacy { get; set; }
        public AccountPrivacyEnum FollowerPrivacy { get; set; }
        public AccountPrivacyEnum FollowingPrivacy { get; set; }
        public GroupChatInvitePermissionEnum GroupChatInvitePermission { get; set; }
    }
}
