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

        public DateTime SentAt { get; set; } = DateTime.Now;
        public bool IsEdited { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public Conversation Conversation { get; set; } = null!;
        public ICollection<MessageMedia> Medias { get; set; } = new List<MessageMedia>();
        public Account Account { get; set; } = null!;
    }
}
 