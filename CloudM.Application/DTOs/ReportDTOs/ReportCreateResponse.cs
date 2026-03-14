namespace CloudM.Application.DTOs.ReportDTOs
{
    public class ReportCreateResponse
    {
        public Guid ModerationReportId { get; set; }
        public string TargetType { get; set; } = string.Empty;
        public Guid TargetId { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string? Detail { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
