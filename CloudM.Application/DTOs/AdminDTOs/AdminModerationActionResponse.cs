namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminModerationActionResponse
    {
        public string Action { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public AdminModerationItemResponse Item { get; set; } = new();
    }
}
