using System;
using System.Collections.Generic;

namespace SocialNetwork.Application.DTOs.ConversationMemberDTOs
{
    public class AddGroupMembersRequest
    {
        public List<Guid> MemberIds { get; set; } = new();
    }
}
