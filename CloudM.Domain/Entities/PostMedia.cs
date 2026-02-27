using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Domain.Entities
{
    public class PostMedia
    {
        public Guid MediaId { get; set; }
        public Guid PostId { get; set; }
        [Required]
        public string MediaUrl { get; set; } = null!;
        public MediaTypeEnum Type { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Post Post { get; set; } = null!;
    }

}

