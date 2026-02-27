using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CloudM.Domain.Enums;

namespace CloudM.Application.DTOs.ConversationDTOs
{
    public class OtherMemberInfo
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? Nickname { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; }
        public StoryRingStateEnum StoryRingState { get; set; } = StoryRingStateEnum.None;
    }
}
