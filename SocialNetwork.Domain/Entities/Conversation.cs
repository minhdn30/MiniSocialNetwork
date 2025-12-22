using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Entities
{
    public class Conversation
    {
        public Guid ConversationId { get; set; }
        public string? ConversationName { get; set; }
        public bool IsGroup { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid CreatedBy { get; set; }
        public bool IsDeleted { get; set; } = false;
        public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual Account CreatedByAccount { get; set; } = null!;

    }
}
