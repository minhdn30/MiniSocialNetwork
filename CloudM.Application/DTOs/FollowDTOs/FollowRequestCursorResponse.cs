namespace CloudM.Application.DTOs.FollowDTOs
{
    public class FollowRequestCursorResponse
    {
        public List<FollowRequestItemResponse> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public FollowRequestNextCursorResponse? NextCursor { get; set; }
    }

    public class FollowRequestNextCursorResponse
    {
        public DateTime CreatedAt { get; set; }
        public Guid RequesterId { get; set; }
    }
}
