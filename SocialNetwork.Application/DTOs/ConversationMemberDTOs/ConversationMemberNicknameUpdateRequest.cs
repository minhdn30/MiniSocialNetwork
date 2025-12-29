using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.ConversationMemberDTOs
{
    public class ConversationMemberNicknameUpdateRequest
    {
        public Guid AccountId { get; set; }
        public string? Nickname { get; set; }
    }
}
