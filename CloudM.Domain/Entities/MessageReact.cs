using CloudM.Domain.Enums;
using System;

namespace CloudM.Domain.Entities
{
    public class MessageReact
    {
        public Guid MessageId { get; set; }
        public Guid AccountId { get; set; }
        public ReactEnum ReactType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual Message Message { get; set; } = null!;
        public virtual Account Account { get; set; } = null!;
    }
}
