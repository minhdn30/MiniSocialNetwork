using Microsoft.AspNetCore.Http;
using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.MessageDTOs
{
    public class SendMessageInPrivateChatRequest
    {
        public Guid ReceiverId { get; set; }
        public string? Content { get; set; }
        public MessageTypeEnum MessageType { get; set; }
        public List<IFormFile>? MediaFiles { get; set; }
    }
}
