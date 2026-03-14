namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminReportCreateRequest
    {
        public string TargetType { get; set; } = string.Empty;
        public Guid TargetId { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
    }
}
