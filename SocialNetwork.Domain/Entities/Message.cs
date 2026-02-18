using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Entities
{
    public class Message
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public Guid AccountId { get; set; }
        public string? Content { get; set; }
        public MessageTypeEnum MessageType { get; set; }
        // Text / Media / System

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsEdited { get; set; } = false;
        public bool IsRecalled { get; set; } = false;
        public DateTime? RecalledAt { get; set; }

        // Reply
        public Guid? ReplyToMessageId { get; set; }

        // Data for System Messages (JSON)
        // VD: {"action": 1, "targetAccountId": "...", "targetUsername": "..."}
        public string? SystemMessageDataJson { get; set; }
        
        // Navigation properties
        public Conversation Conversation { get; set; } = null!;
        public Account Account { get; set; } = null!;
        public Message? ReplyToMessage { get; set; }
        public ICollection<MessageMedia> Medias { get; set; } = new List<MessageMedia>();
        public ICollection<MessageReact> Reacts { get; set; } = new List<MessageReact>();
        public ICollection<MessageHidden> HiddenBy { get; set; } = new List<MessageHidden>();
    }
}