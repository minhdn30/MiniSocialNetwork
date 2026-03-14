namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminAccountLookupResponse
    {
        public string Keyword { get; set; } = string.Empty;
        public int TotalResults { get; set; }
        public List<AdminAccountLookupItemResponse> Items { get; set; } = new();
    }
}
