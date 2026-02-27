using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.ConversationDTOs
{
    public class CreatePrivateConversationRequest
    {
        [Required]
        public Guid OtherId { get; set; }
    }
}
