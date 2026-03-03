using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Domain.Entities
{
    public class PostSave
    {
        public Guid PostId { get; set; }
        public Guid AccountId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Post Post { get; set; } = null!;
        public virtual Account Account { get; set; } = null!;
    }
}
