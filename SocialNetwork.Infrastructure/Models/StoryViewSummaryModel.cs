namespace SocialNetwork.Infrastructure.Models
{
    public class StoryViewSummaryModel
    {
        public Guid StoryId { get; set; }
        public int TotalViews { get; set; }
        public IReadOnlyList<StoryViewerBasicModel> TopViewers { get; set; } = Array.Empty<StoryViewerBasicModel>();
    }
}
