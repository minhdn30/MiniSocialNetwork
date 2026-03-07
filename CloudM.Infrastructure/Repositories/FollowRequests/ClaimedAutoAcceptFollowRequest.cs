namespace CloudM.Infrastructure.Repositories.FollowRequests
{
    public class ClaimedAutoAcceptFollowRequest
    {
        public Guid RequesterId { get; set; }
        public Guid TargetId { get; set; }
        public int RequesterStatus { get; set; }
    }
}
