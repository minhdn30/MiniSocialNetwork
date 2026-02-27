namespace CloudM.Application.DTOs.AuthDTOs
{
    public class ExternalLoginSummaryResponse
    {
        public string Provider { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}
