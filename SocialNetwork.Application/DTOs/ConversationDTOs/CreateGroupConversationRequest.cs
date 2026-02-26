using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace SocialNetwork.Application.DTOs.ConversationDTOs
{
    public class CreateGroupConversationRequest
    {
        public string GroupName { get; set; } = string.Empty;
        public IFormFile? GroupAvatar { get; set; }
        public List<Guid> MemberIds { get; set; } = new();
    }
}
