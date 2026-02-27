using System;
using System.Collections.Generic;

namespace CloudM.Application.DTOs.ConversationMemberDTOs
{
    public class AddGroupMembersRequest
    {
        public List<Guid> MemberIds { get; set; } = new();
    }
}
