namespace CloudM.Application.DTOs.FollowDTOs
{
    public class FollowRequestCursorRequest
    {
        public int Limit { get; set; } = 20;
        public DateTime? CursorCreatedAt { get; set; }
        public Guid? CursorRequesterId { get; set; }
    }
}
