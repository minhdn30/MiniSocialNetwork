using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AccountBlocks
{
    public interface IAccountBlockRepository
    {
        Task<bool> IsBlockedByCurrentUserAsync(Guid currentId, Guid targetId);
        Task<bool> IsBlockedEitherWayAsync(Guid currentId, Guid targetId);
        Task<bool> HasAnyRelationWithinAsync(
            IEnumerable<Guid> accountIds,
            IEnumerable<Guid>? focusAccountIds = null,
            CancellationToken cancellationToken = default);
        Task<List<AccountBlockPairRelationModel>> GetRelationPairsAsync(
            IEnumerable<Guid> currentIds,
            IEnumerable<Guid> targetIds,
            CancellationToken cancellationToken = default);
        Task AddAsync(AccountBlock block);
        Task<bool> AddIgnoreExistingAsync(AccountBlock block, CancellationToken cancellationToken = default);
        Task<int> RemoveAsync(Guid blockerId, Guid blockedId);
        Task<List<AccountBlockRelationModel>> GetRelationsAsync(Guid currentId, IEnumerable<Guid> targetIds, CancellationToken cancellationToken = default);
        Task<(List<BlockedAccountListItemModel> Items, int TotalItems)> GetBlockedAccountsAsync(
            Guid blockerId,
            string? keyword,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
