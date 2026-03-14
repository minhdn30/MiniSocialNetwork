namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminAuditLogItemResponse
    {
        public Guid AdminAuditLogId { get; set; }
        public Guid AdminId { get; set; }
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminFullname { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string? RequestIp { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
