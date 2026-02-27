using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.ConversationDTOs
{
    public class PrivateChatListItemResponse
    {
        public Guid ConversationId { get; set; }
        public OtherMemberInfo OtherMember { get; set; } = null!;

        public MessageBasicModel? LastMessage { get; set; }
        public string? LastMessagePreview { get; set; }

        public bool IsRead { get; set; }
        public int UnreadCount { get; set; }
    }
}
