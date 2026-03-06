using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.ConversationDTOs
{
    public class GroupChatListItemResponse
    {
        public Guid ConversationId { get; set; }
        public string? ConversationName { get; set; }
        public string? ConversationAvatar { get; set; }

        public MessageBasicModel? LastMessage { get; set; }
        public string? LastMessagePreview { get; set; }

        public bool IsRead { get; set; }
        public int UnreadCount { get; set; }
    }
}
