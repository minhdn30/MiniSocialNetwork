using System;
using System.Collections.Generic;

namespace CloudM.Application.DTOs.PresenceDTOs
{
    public class PresenceSnapshotRequest
    {
        public List<Guid> AccountIds { get; set; } = new();
    }
}
