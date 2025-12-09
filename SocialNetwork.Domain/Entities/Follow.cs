using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Entities
{
    public class Follow
    {
        public Guid FollowerId { get; set; } //the one who follows
        public Guid FollowedId { get; set; } //the one being followed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual Account Follower { get; set; }
        public virtual Account Followed { get; set; }
    }
}
