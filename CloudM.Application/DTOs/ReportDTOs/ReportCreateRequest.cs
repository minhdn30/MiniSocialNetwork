namespace CloudM.Application.DTOs.ReportDTOs
{
    public class ReportCreateRequest
    {
        public string TargetType { get; set; } = string.Empty;
        public Guid TargetId { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string? Detail { get; set; }
    }
}
