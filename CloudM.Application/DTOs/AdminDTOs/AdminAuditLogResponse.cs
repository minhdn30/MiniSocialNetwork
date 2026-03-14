namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminAuditLogResponse
    {
        public int TotalResults { get; set; }
        public int AppliedLimit { get; set; }
        public string Module { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public List<AdminAuditLogItemResponse> Items { get; set; } = new();
    }
}
