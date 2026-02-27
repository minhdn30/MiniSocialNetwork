using CloudM.Application.DTOs.ConversationMemberDTOs;
using CloudM.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.ConversationDTOs
{
    public class ConversationResponse
    {
        public Guid ConversationId { get; set; }
        public string? ConversationName { get; set; }
        public string? ConversationAvatar { get; set; }
        public string? Theme { get; set; }
        public bool IsGroup { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid? Owner { get; set; }
        public bool IsDeleted { get; set; } = false;
        public IEnumerable<ConversationMemberResponse> Members { get; set; } = new List<ConversationMemberResponse>();
    }
}
