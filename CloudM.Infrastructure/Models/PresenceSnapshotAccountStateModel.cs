using CloudM.Domain.Enums;

namespace CloudM.Infrastructure.Models
{
    public class PresenceSnapshotAccountStateModel
    {
        public Guid AccountId { get; set; }
        public DateTime? LastOnlineAt { get; set; }
        public OnlineStatusVisibilityEnum Visibility { get; set; }
    }
}
