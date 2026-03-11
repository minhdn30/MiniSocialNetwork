using System.Text.Json.Serialization;

namespace CloudM.Infrastructure.Models
{
    public class PostFeedCursorModel
    {
        public DateTime SnapshotAt { get; set; }
        public string ProfileKey { get; set; } = string.Empty;
        public long SessionSeed { get; set; }
        public decimal? Score { get; set; }
        public long? JitterRank { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Guid? PostId { get; set; }
        public DateTime? WindowCursorCreatedAt { get; set; }
        public Guid? WindowCursorPostId { get; set; }

        [JsonIgnore]
        public bool HasPosition =>
            Score.HasValue &&
            JitterRank.HasValue &&
            CreatedAt.HasValue &&
            PostId.HasValue;

        [JsonIgnore]
        public bool HasPartialPosition =>
            Score.HasValue ||
            JitterRank.HasValue ||
            CreatedAt.HasValue ||
            PostId.HasValue;

        [JsonIgnore]
        public bool HasWindowCursor =>
            WindowCursorCreatedAt.HasValue &&
            WindowCursorPostId.HasValue;

        [JsonIgnore]
        public bool HasPartialWindowCursor =>
            WindowCursorCreatedAt.HasValue != WindowCursorPostId.HasValue;
    }
}
