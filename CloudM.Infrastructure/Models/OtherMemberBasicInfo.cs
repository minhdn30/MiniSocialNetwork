using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Models
{
    public class OtherMemberBasicInfo
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
