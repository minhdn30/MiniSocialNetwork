using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class ConversationListModel
    {
        public Guid ConversationId { get; set; }
        public bool IsGroup { get; set; }
        
        // Flattened for easy UI access
        public string? DisplayName { get; set; }
        public string? DisplayAvatar { get; set; }
        
        // For Private Chat
        public OtherMemberBasicInfo? OtherMember { get; set; }
        
        // For Group Chat
        public string? ConversationName { get; set; }
        public string? ConversationAvatar { get; set; }
        public string? Theme { get; set; }
        public Guid? Owner { get; set; }
        public int CurrentUserRole { get; set; }
        // Avatars of group members (max 4, excluding current user)
        public List<string>? GroupAvatars { get; set; }

        public MessageBasicModel? LastMessage { get; set; }
        public bool IsRead { get; set; }
        public int UnreadCount { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public DateTime? LastMessageSentAt { get; set; }

        // Members who have seen the last message (only when last message is from current user)
        // Max 3 entries for sidebar display
        public List<SeenByMemberInfo>? LastMessageSeenBy { get; set; }

        // Total number of members who have seen the last message (excluding current user)
        public int LastMessageSeenCount { get; set; }
        public bool IsMuted { get; set; }
    }

    public class SeenByMemberInfo
    {
        public Guid AccountId { get; set; }
        public string? AvatarUrl { get; set; }
        public string? DisplayName { get; set; }
    }
}
