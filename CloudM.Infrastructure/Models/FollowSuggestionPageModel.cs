using System.Collections.Generic;

namespace CloudM.Infrastructure.Models
{
    public class FollowSuggestionPageModel : FollowSuggestionModel
    {
        public bool IsContact { get; set; }
        public bool IsFollower { get; set; }
        public int MutualFollowCount { get; set; }
        public List<string> MutualFollowPreviewUsernames { get; set; } = new();
    }
}
