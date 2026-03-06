using CloudM.Domain.Entities;

namespace CloudM.Infrastructure.Repositories.FollowRequests
{
    public interface IFollowRequestRepository
    {
        Task<bool> IsFollowRequestExistAsync(Guid requesterId, Guid targetId);
        Task AddFollowRequestAsync(FollowRequest followRequest);
        Task RemoveFollowRequestAsync(Guid requesterId, Guid targetId);
    }
}
