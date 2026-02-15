using System;

namespace SocialNetwork.Application.DTOs.AccountDTOs
{
    public class AccountChatInfoResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; }
    }
}
