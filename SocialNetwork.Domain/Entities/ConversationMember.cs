using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Entities
{
    public class ConversationMember
    {
        public Guid ConversationId { get; set; }
        public Guid AccountId { get; set; }
        public string? Nickname { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool IsAdmin { get; set; } = false;
        public bool HasLeft { get; set; } = false;
        public Guid? LastSeenMessageId { get; set; }
        public bool IsMuted { get; set; } = false; // mute notification
        public bool IsDeleted { get; set; } = false;
        public DateTime? ClearedAt { get; set; } 
        public Conversation Conversation { get; set; } = null!;
        public Account Account { get; set; } = null!;
    }
}
