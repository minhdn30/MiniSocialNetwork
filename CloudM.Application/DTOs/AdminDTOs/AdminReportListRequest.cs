namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminReportListRequest
    {
        public string Status { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public int Limit { get; set; } = 12;
    }
}
