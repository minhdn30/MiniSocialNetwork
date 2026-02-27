namespace CloudM.Infrastructure.Models
{
    public class StoryAuthorVisibleSummaryModel
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public DateTime LatestStoryCreatedAt { get; set; }
        public int ActiveStoryCount { get; set; }
        public int UnseenCount { get; set; }
        public int ViewFrequencyScore { get; set; }
    }
}
