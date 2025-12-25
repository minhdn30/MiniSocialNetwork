using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.ConversationDTOs
{
    public class ConversationGetOrCreateRequest
    {
        [Required]
        public Guid ReceiverId { get; set; }
    }
}
