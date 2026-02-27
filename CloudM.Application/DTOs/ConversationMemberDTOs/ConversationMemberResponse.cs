using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.ConversationMemberDTOs
{
    public class ConversationMemberResponse
    {
        public Guid ConversationId { get; set; }
        public AccountBasicInfoResponse Account { get; set; } = null!;
        public string? Nickname { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsAdmin { get; set; }
        public bool HasLeft { get; set; }
        public Guid? LastSeenMessageId { get; set; }
        public bool IsMuted { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? ClearedAt { get; set; }
    }
}
