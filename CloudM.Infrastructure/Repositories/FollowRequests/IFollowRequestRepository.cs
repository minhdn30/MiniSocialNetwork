using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.FollowRequests
{
    public interface IFollowRequestRepository
    {
        Task<bool> IsFollowRequestExistAsync(Guid requesterId, Guid targetId);
        Task AddFollowRequestAsync(FollowRequest followRequest);
        Task<bool> AddFollowRequestIgnoreExistingAsync(FollowRequest followRequest, CancellationToken cancellationToken = default);
        Task<int> RemoveFollowRequestAsync(Guid requesterId, Guid targetId);
        Task<(List<PendingFollowRequestListItem> Items, DateTime? NextCursorCreatedAt, Guid? NextCursorRequesterId)> GetPendingByTargetAsync(
            Guid targetId,
            int limit,
            DateTime? cursorCreatedAt,
            Guid? cursorRequesterId,
            CancellationToken cancellationToken = default);
        Task<(List<AccountWithFollowStatusModel> Items, int TotalItems)> GetPendingSentByRequesterAsync(
            Guid requesterId,
            string? keyword,
            bool? sortByCreatedASC,
            int page,
            int pageSize);
        Task<int> GetPendingCountByTargetAsync(Guid targetId, CancellationToken cancellationToken = default);
        Task<List<ClaimedAutoAcceptFollowRequest>> ClaimAutoAcceptBatchAsync(int batchSize, CancellationToken cancellationToken = default);
    }
}
