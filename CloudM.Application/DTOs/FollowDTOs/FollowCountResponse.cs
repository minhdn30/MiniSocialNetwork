using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CloudM.Domain.Enums;

namespace CloudM.Application.DTOs.FollowDTOs
{
    public class FollowCountResponse
    {
        public int Followers { get; set; }
        public int Following { get; set; }
        public bool IsFollowedByCurrentUser { get; set; } = false;
        public bool IsFollowRequestPendingByCurrentUser { get; set; } = false;
        public FollowRelationStatusEnum RelationStatus { get; set; } = FollowRelationStatusEnum.None;
        public FollowPrivacyEnum TargetFollowPrivacy { get; set; } = FollowPrivacyEnum.Anyone;
    }
}
