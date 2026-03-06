using System;

namespace CloudM.Domain.Entities
{
    public class FollowRequest
    {
        public Guid RequesterId { get; set; } // the one who requests to follow
        public Guid TargetId { get; set; } // the one who receives the request
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual Account Requester { get; set; } = null!;
        public virtual Account Target { get; set; } = null!;
    }
}
