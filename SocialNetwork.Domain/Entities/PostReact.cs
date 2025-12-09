using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Entities
{
    public class PostReact
    {
        public Guid PostId { get; set; }
        public Guid AccountId { get; set; }
        public ReactEnum ReactType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual Post Post { get; set; } = null!;
        public virtual Account Account { get; set; } = null!;
    }

}
