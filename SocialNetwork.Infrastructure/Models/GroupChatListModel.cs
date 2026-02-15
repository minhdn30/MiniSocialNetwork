using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class GroupChatListModel
    {
        public Guid ConversationId { get; set; }
        public string? ConversationName { get; set; }
        public string? ConversationAvatar { get; set; }

        public MessageBasicModel? LastMessage { get; set; }

        public bool IsRead { get; set; }
        public int UnreadCount { get; set; }
        public DateTime? LastSeenAt { get; set; }
    }
}
