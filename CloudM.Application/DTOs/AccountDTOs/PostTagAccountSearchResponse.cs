using System;

namespace CloudM.Application.DTOs.AccountDTOs
{
    public class PostTagAccountSearchResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public bool IsFollowing { get; set; }
        public bool IsFollower { get; set; }
        public DateTime? LastContactedAt { get; set; }
        public double MatchScore { get; set; }
        public double FollowingScore { get; set; }
        public double FollowerScore { get; set; }
        public double RecentChatScore { get; set; }
        public double TotalScore { get; set; }
    }
}
