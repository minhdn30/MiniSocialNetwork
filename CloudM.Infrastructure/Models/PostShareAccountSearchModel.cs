using System;

namespace CloudM.Infrastructure.Models
{
    public class PostShareAccountSearchModel
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public bool IsContacted { get; set; }
        public DateTime? LastContactedAt { get; set; }
        public double MatchScore { get; set; }
    }
}
