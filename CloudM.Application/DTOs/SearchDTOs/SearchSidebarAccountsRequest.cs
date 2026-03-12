namespace CloudM.Application.DTOs.SearchDTOs
{
    public class SearchSidebarAccountsRequest
    {
        public string? Keyword { get; set; }
        public int Limit { get; set; } = 20;
    }
}
