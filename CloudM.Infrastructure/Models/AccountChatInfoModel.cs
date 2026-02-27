using System;

namespace CloudM.Infrastructure.Models
{
    public class AccountChatInfoModel
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; }
    }
}
