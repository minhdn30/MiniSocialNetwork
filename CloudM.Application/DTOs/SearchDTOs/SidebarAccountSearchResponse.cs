namespace CloudM.Application.DTOs.SearchDTOs
{
    public class SidebarAccountSearchResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public DateTime? LastSearchedAt { get; set; }
    }
}
