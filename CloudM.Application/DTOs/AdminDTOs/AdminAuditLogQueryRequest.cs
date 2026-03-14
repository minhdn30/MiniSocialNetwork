namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminAuditLogQueryRequest
    {
        public int Limit { get; set; } = 20;
        public string Module { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
    }
}
