using System;

namespace CloudM.Application.DTOs.ConversationDTOs
{
    public class GroupInviteAccountSearchResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public bool IsFollowing { get; set; }
        public bool IsFollower { get; set; }
        public int MutualGroupCount { get; set; }
        public DateTime? LastDirectMessageAt { get; set; }
        public double MatchScore { get; set; }
        public double FollowingScore { get; set; }
        public double FollowerScore { get; set; }
        public double RecentChatScore { get; set; }
        public double MutualGroupScore { get; set; }
        public double TotalScore { get; set; }
    }
}
