namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminReportListResponse
    {
        public string Status { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public int AppliedLimit { get; set; }
        public int TotalResults { get; set; }
        public List<AdminReportItemResponse> Items { get; set; } = new();
    }
}
