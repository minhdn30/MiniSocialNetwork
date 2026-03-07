using System;

namespace CloudM.Infrastructure.Models
{
    public class PostTaggedAccountModel
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool IsFollowing { get; set; }
        public bool IsFollowRequested { get; set; }
        public bool IsFollower { get; set; }
    }
}
