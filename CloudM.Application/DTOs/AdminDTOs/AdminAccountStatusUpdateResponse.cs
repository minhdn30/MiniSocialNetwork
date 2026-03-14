namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminAccountStatusUpdateResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Fullname { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string PreviousStatus { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool RequiresSignOut { get; set; }
    }
}
