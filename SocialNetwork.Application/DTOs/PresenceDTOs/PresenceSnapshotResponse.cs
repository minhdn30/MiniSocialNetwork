using System.Collections.Generic;

namespace SocialNetwork.Application.DTOs.PresenceDTOs
{
    public class PresenceSnapshotResponse
    {
        public List<PresenceSnapshotItemResponse> Items { get; set; } = new();
    }
}
