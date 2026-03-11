namespace CloudM.Application.DTOs.FollowDTOs
{
    public class FollowSuggestionPagingRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Surface { get; set; } = "page";
    }
}
