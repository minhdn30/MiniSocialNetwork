using System;

namespace CloudM.Domain.Entities
{
    public class PostTag
    {
        public Guid PostId { get; set; }
        public Guid TaggedAccountId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Post Post { get; set; } = null!;
        public virtual Account TaggedAccount { get; set; } = null!;
    }
}
