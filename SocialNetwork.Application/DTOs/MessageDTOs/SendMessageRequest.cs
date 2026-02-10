using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.MessageDTOs
{
    public class SendMessageRequest
    {
        public string? Content { get; set; }
        public List<IFormFile>? MediaFiles { get; set; }
    }
}
