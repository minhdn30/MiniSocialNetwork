using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.ConversationDTOs
{
    public class ConversationMessagesResponse
    {
        public ConversationMetaData? MetaData { get; set; }
        public CursorResponse<MessageBasicModel> Messages { get; set; } = null!;
    }

    public class ConversationMetaData
    {
        public Guid ConversationId { get; set; }
        public bool IsGroup { get; set; }
        public bool IsMuted { get; set; }
        public string? Theme { get; set; }
        public string? DisplayName { get; set; }
        public string? DisplayAvatar { get; set; }
        public Guid? Owner { get; set; }
        public int CurrentUserRole { get; set; }
        public int MemberCount { get; set; }
        public OtherMemberInfo? OtherMember { get; set; }
        public List<string>? SampleMembers { get; set; } 
        public List<string>? GroupAvatars { get; set; }
        public List<ConversationMemberInfo> Members { get; set; } = new();
        public List<MemberSeenStatus> MemberSeenStatuses { get; set; } = new();
    }

    public class ConversationMemberInfo
    {
        public Guid AccountId { get; set; }
        public string? AvatarUrl { get; set; }
        public string? DisplayName { get; set; }
        public string? Username { get; set; }
        public string? Nickname { get; set; }
        public int Role { get; set; }
    }

    public class MemberSeenStatus
    {
        public Guid AccountId { get; set; }
        public string? AvatarUrl { get; set; }
        public string? DisplayName { get; set; }
        public Guid? LastSeenMessageId { get; set; }
    }
}
