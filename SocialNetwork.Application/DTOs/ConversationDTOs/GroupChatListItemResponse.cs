using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.ConversationDTOs
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
