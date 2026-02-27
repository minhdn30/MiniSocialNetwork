namespace SocialNetwork.Infrastructure.Models
{
    public class StoryReplyInfoModel
    {
        public Guid StoryId { get; set; }
        public bool IsStoryExpired { get; set; }
        public string? MediaUrl { get; set; }
        public int ContentType { get; set; }
        public string? TextContent { get; set; }
        public string? BackgroundColorKey { get; set; }
        public string? TextColorKey { get; set; }
        public string? FontTextKey { get; set; }
        public string? FontSizeKey { get; set; }
    }
}
