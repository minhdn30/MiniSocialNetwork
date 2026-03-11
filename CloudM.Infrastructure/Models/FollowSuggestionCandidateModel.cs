namespace CloudM.Infrastructure.Models
{
    public class FollowSuggestionCandidateModel
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public bool IsContact { get; set; }
        public bool IsFollower { get; set; }
        public int MutualFollowCount { get; set; }
    }
}
