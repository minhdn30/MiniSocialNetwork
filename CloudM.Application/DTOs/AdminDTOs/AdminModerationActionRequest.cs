namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminModerationActionRequest
    {
        public string Action { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
