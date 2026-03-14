namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminSessionResponse
    {
        public Guid AccountId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Fullname { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
