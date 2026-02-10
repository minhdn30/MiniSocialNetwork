using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.ConversationDTOs
{
    public class ConversationListItemResponse
    {
        public Guid ConversationId { get; set; }
        public bool IsGroup { get; set; }
        
        // Common display info
        public string? DisplayName { get; set; }
        public string? DisplayAvatar { get; set; }

        // Specific for Private
        public OtherMemberInfo? OtherMember { get; set; }
        public MessageBasicModel? LastMessage { get; set; }
        public string? LastMessagePreview { get; set; }
        public bool IsRead { get; set; }
        public int UnreadCount { get; set; }
        public DateTime? LastMessageSentAt { get; set; }

        /// <summary>
        /// Members who seen the last message (only populated when the current user sent the last message).
        /// </summary>
        public List<SeenByMemberInfo>? LastMessageSeenBy { get; set; }
    }
}
