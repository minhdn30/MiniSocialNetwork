namespace CloudM.Application.DTOs.MessageDTOs
{
    public class SendStoryReplyRequest
    {
        public Guid ReceiverId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? TempId { get; set; }

        // Story snapshot (stored in SystemMessageDataJson)
        public Guid StoryId { get; set; }
        public string? StoryMediaUrl { get; set; }
        public int StoryContentType { get; set; }
        public string? StoryTextContent { get; set; }
        public string? StoryBackgroundColorKey { get; set; }
        public string? StoryTextColorKey { get; set; }
        public string? StoryFontTextKey { get; set; }
        public string? StoryFontSizeKey { get; set; }
    }
}
