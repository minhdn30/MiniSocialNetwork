using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Models
{
    public class PostPersonalListModel
    {
        public Guid PostId { get; set; }
        public string PostCode { get; set; } = string.Empty;
        public List<MediaPostPersonalListModel>? Medias { get; set; } = new();
        public int MediaCount { get; set; }
        public int ReactCount { get; set; }
        public int CommentCount { get; set; }
    }
}
