using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Infrastructure.Models
{
    public class MessageReactAccountModel
    {
        public Guid AccountId { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Nickname { get; set; }
        public ReactEnum ReactType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
