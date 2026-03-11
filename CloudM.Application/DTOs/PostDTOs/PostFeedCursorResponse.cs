using CloudM.Infrastructure.Models;

namespace CloudM.Application.DTOs.PostDTOs
{
    public class PostFeedCursorResponse
    {
        public List<PostFeedModel> Items { get; set; } = new();
        public PostFeedNextCursorResponse? NextCursor { get; set; }
    }

    public class PostFeedNextCursorResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid PostId { get; set; }
    }
}
