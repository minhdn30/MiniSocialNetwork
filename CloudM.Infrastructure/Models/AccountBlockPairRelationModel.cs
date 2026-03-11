namespace CloudM.Infrastructure.Models
{
    public class AccountBlockPairRelationModel
    {
        public Guid CurrentId { get; set; }
        public Guid TargetId { get; set; }
        public bool IsBlockedByCurrentUser { get; set; }
        public bool IsBlockedByTargetUser { get; set; }
        public bool IsBlockedEitherWay => IsBlockedByCurrentUser || IsBlockedByTargetUser;
    }
}
