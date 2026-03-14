using CloudM.Domain.Enums;

namespace CloudM.Infrastructure.Models
{
    public class AdminAccountLookupItemModel
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public AccountStatusEnum Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastOnlineAt { get; set; }
    }
}
