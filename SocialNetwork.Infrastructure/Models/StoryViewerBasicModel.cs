using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Infrastructure.Models
{
    public class StoryViewerBasicModel
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public DateTime ViewedAt { get; set; }
        public ReactEnum? ReactType { get; set; }
    }
}
