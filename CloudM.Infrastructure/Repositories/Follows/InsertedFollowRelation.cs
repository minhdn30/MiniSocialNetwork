namespace CloudM.Infrastructure.Repositories.Follows
{
    public class InsertedFollowRelation
    {
        public Guid FollowerId { get; set; }
        public Guid FollowedId { get; set; }
    }
}
