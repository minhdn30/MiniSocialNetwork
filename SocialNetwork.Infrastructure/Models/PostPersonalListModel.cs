using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class PostPersonalListModel
    {
        public Guid PostId { get; set; }
        public List<MediaPostPersonalListModel>? Medias { get; set; } = new();
        public int MediaCount { get; set; }
        public int ReactCount { get; set; }
        public int CommentCount { get; set; }
    }
}
