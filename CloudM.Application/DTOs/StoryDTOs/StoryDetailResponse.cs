namespace CloudM.Application.DTOs.StoryDTOs
{
    public class StoryDetailResponse
    {
        public Guid StoryId { get; set; }
        public Guid AccountId { get; set; }
        public int ContentType { get; set; }
        public string? MediaUrl { get; set; }
        public string? TextContent { get; set; }
        public string? BackgroundColorKey { get; set; }
        public string? FontTextKey { get; set; }
        public string? FontSizeKey { get; set; }
        public string? TextColorKey { get; set; }
        public int Privacy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
