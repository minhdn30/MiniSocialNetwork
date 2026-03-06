namespace CloudM.Application.DTOs.StoryDTOs
{
    public class StoryMarkViewedResponse
    {
        public int RequestedCount { get; set; }
        public int VisibleCount { get; set; }
        public int MarkedCount { get; set; }
        public int AlreadyViewedCount => Math.Max(0, VisibleCount - MarkedCount);
    }
}
