using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CloudM.Domain.Enums;

namespace CloudM.Application.DTOs.AccountDTOs
{
    public class ProfileDetailResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string? Email { get; set; }
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
        public bool? Gender { get; set; }
        public string? Address { get; set; }
        public string? Bio { get; set; }
        public string? CoverUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public StoryRingStateEnum StoryRingState { get; set; } = StoryRingStateEnum.None;
    }
}
