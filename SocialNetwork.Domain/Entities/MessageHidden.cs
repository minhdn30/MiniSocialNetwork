using System;

namespace SocialNetwork.Domain.Entities
{
    // Track messages hidden by each member.
    // Different from recall - messages are still visible to other members.
    public class MessageHidden
    {
        public Guid MessageId { get; set; }
        public Guid AccountId { get; set; }  // Member who hid the message
        public DateTime HiddenAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual Message Message { get; set; } = null!;
        public virtual Account Account { get; set; } = null!;
    }
}
