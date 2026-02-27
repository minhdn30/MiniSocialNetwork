using System;

namespace CloudM.Application.DTOs.PresenceDTOs
{
    public class PresenceSnapshotItemResponse
    {
        public Guid AccountId { get; set; }
        public bool CanShowStatus { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastOnlineAt { get; set; }
    }
}
