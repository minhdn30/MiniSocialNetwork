using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class MessageBasicModel
    {
        public Guid MessageId { get; set; }
        public AccountBasicInfoModel Sender { get; set; } = null!;
        public string? Content { get; set; }
        public MessageTypeEnum MessageType { get; set; } // Text / Media / System
        public DateTime SentAt { get; set; }
        public bool IsEdited { get; set; }
        public bool IsDeleted { get; set; }
        public List<MessageMediaBasicModel>? Medias { get; set; }
    }
}
