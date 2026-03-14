namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminReportItemResponse
    {
        public Guid ModerationReportId { get; set; }
        public string TargetType { get; set; } = string.Empty;
        public Guid TargetId { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string? Detail { get; set; }
        public string Status { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string CreatedByAdminEmail { get; set; } = string.Empty;
        public string CreatedByAdminFullname { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
