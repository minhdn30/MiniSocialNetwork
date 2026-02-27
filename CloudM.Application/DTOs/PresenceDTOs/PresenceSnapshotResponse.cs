using System.Collections.Generic;

namespace CloudM.Application.DTOs.PresenceDTOs
{
    public class PresenceSnapshotResponse
    {
        public List<PresenceSnapshotItemResponse> Items { get; set; } = new();
    }
}
