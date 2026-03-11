namespace CloudM.Infrastructure.Models
{
    public class AccountBlockRelationModel
    {
        public Guid TargetId { get; set; }
        public bool IsBlockedByCurrentUser { get; set; }
        public bool IsBlockedByTargetUser { get; set; }
        public bool IsBlockedEitherWay => IsBlockedByCurrentUser || IsBlockedByTargetUser;
    }
}
