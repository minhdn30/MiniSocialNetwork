using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Models
{
    public class PrivateChatListModel
    {
        public Guid ConversationId { get; set; }
        public OtherMemberBasicInfo OtherMember { get; set; } = null!;

        public MessageBasicModel? LastMessage { get; set; }

        public bool IsRead { get; set; }
        public int UnreadCount { get; set; }
        public DateTime? LastSeenAt { get; set; }
    }
}
