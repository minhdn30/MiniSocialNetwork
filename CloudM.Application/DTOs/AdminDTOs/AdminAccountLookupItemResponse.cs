namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminAccountLookupItemResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Fullname { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsEmailVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastOnlineAt { get; set; }
    }
}
