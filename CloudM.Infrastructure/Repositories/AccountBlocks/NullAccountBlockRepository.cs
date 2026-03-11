using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AccountBlocks
{
    public sealed class NullAccountBlockRepository : IAccountBlockRepository
    {
        public static readonly NullAccountBlockRepository Instance = new();

        private NullAccountBlockRepository()
        {
        }

        public Task AddAsync(AccountBlock block)
        {
            return Task.CompletedTask;
        }

        public Task<bool> AddIgnoreExistingAsync(AccountBlock block, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<(List<BlockedAccountListItemModel> Items, int TotalItems)> GetBlockedAccountsAsync(
            Guid blockerId,
            string? keyword,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((new List<BlockedAccountListItemModel>(), 0));
        }

        public Task<List<AccountBlockPairRelationModel>> GetRelationPairsAsync(
            IEnumerable<Guid> currentIds,
            IEnumerable<Guid> targetIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<AccountBlockPairRelationModel>());
        }

        public Task<List<AccountBlockRelationModel>> GetRelationsAsync(Guid currentId, IEnumerable<Guid> targetIds, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<AccountBlockRelationModel>());
        }

        public Task<bool> IsBlockedByCurrentUserAsync(Guid currentId, Guid targetId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> IsBlockedEitherWayAsync(Guid currentId, Guid targetId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> HasAnyRelationWithinAsync(
            IEnumerable<Guid> accountIds,
            IEnumerable<Guid>? focusAccountIds = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<int> RemoveAsync(Guid blockerId, Guid blockedId)
        {
            return Task.FromResult(0);
        }
    }
}
