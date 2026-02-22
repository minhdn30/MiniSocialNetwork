using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Infrastructure.Models
{
    public class PresenceSnapshotAccountStateModel
    {
        public Guid AccountId { get; set; }
        public DateTime? LastOnlineAt { get; set; }
        public OnlineStatusVisibilityEnum Visibility { get; set; }
    }
}
