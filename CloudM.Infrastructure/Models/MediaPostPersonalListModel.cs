using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Models
{
    public class MediaPostPersonalListModel
    {
        public Guid MediaId { get; set; }
        public string MediaUrl { get; set; } = null!;
        public MediaTypeEnum Type { get; set; }
    }
}
