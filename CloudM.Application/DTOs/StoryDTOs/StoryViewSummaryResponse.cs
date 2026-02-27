namespace CloudM.Application.DTOs.StoryDTOs
{
    public class StoryViewSummaryResponse
    {
        public int TotalViews { get; set; }
        public IReadOnlyList<StoryViewerBasicResponse> TopViewers { get; set; } = Array.Empty<StoryViewerBasicResponse>();
    }
}
