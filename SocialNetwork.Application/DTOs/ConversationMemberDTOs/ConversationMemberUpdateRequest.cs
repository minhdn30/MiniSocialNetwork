using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.ConversationMemberDTOs
{
    public class ConversationMemberUpdateRequest
    {
        public Guid AccountId { get; set; }
        public string? Nickname { get; set; }
        public bool? IsAdmin { get; set; }
        public bool? HasLeft { get; set; }
        public bool? IsMuted { get; set; }
        public bool? IsDeleted { get; set; }
    }
}
