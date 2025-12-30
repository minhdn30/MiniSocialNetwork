using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.MessageMediaDTOs;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.MessageDTOs
{
    public class SendMessageResponse
    {
        public Guid MessageId { get; set; }
        public AccountBasicInfoResponse Sender { get; set; } = null!;
        public string? Content { get; set; }
        public MessageTypeEnum MessageType { get; set; } // Text / Media / System
        public DateTime SentAt { get; set; }
        public bool IsEdited { get; set; }
        public bool IsDeleted { get; set; }
        public List<MessageMediaResponse>? Medias { get; set; }
    }
}
