namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminModerationLookupResponse
    {
        public string TargetType { get; set; } = string.Empty;
        public string Keyword { get; set; } = string.Empty;
        public AdminModerationItemResponse? Item { get; set; }
    }
}
