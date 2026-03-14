namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminAuditLogWriteRequest
    {
        public Guid AdminId { get; set; }
        public string Module { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string? RequestIp { get; set; }
    }
}
