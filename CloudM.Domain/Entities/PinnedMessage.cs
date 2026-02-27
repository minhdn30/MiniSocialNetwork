using System;

namespace CloudM.Domain.Entities
{
    public class PinnedMessage
    {
        public Guid ConversationId { get; set; }
        public Guid MessageId { get; set; }
        public Guid PinnedBy { get; set; }
        public DateTime PinnedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Conversation Conversation { get; set; } = null!;
        public Message Message { get; set; } = null!;
        public Account PinnedByAccount { get; set; } = null!;
    }
}
