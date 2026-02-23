namespace SocialNetwork.Infrastructure.Models
{
    public class StoryRingStatsByAuthorModel
    {
        public Guid AccountId { get; set; }
        public int VisibleCount { get; set; }
        public int UnseenCount { get; set; }
    }
}
