using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CloudM.Infrastructure.Repositories.AccountBlocks
{
    public class AccountBlockRepository : IAccountBlockRepository
    {
        private readonly AppDbContext _context;

        public AccountBlockRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task AddAsync(AccountBlock block)
        {
            _context.AccountBlocks.Add(block);
            return Task.CompletedTask;
        }

        public async Task<bool> AddIgnoreExistingAsync(AccountBlock block, CancellationToken cancellationToken = default)
        {
            if (block == null ||
                block.BlockerId == Guid.Empty ||
                block.BlockedId == Guid.Empty ||
                block.BlockerId == block.BlockedId)
            {
                return false;
            }

            var blockerIdParam = new NpgsqlParameter<Guid>("p_blocker_id", block.BlockerId);
            var blockedIdParam = new NpgsqlParameter<Guid>("p_blocked_id", block.BlockedId);
            var createdAtParam = new NpgsqlParameter<DateTime>("p_created_at", NpgsqlDbType.TimestampTz)
            {
                TypedValue = DateTime.SpecifyKind(
                    block.CreatedAt == default ? DateTime.UtcNow : block.CreatedAt,
                    DateTimeKind.Utc)
            };
            var blockerSnapshotParam = new NpgsqlParameter<string?>("p_blocker_snapshot_username", block.BlockerSnapshotUsername);
            var blockedSnapshotParam = new NpgsqlParameter<string?>("p_blocked_snapshot_username", block.BlockedSnapshotUsername);

            var affected = await _context.Database.ExecuteSqlRawAsync(@"
INSERT INTO ""AccountBlocks"" (
    ""BlockerId"",
    ""BlockedId"",
    ""CreatedAt"",
    ""BlockerSnapshotUsername"",
    ""BlockedSnapshotUsername"")
VALUES (
    @p_blocker_id,
    @p_blocked_id,
    @p_created_at,
    @p_blocker_snapshot_username,
    @p_blocked_snapshot_username)
ON CONFLICT (""BlockerId"", ""BlockedId"") DO NOTHING;",
                new object[]
                {
                    blockerIdParam,
                    blockedIdParam,
                    createdAtParam,
                    blockerSnapshotParam,
                    blockedSnapshotParam
                },
                cancellationToken);

            return affected > 0;
        }

        public async Task<(List<BlockedAccountListItemModel> Items, int TotalItems)> GetBlockedAccountsAsync(
            Guid blockerId,
            string? keyword,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var safePage = page <= 0 ? 1 : page;
            var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 50);

            var query = _context.AccountBlocks
                .AsNoTracking()
                .Where(x => x.BlockerId == blockerId)
                .Select(x => new
                {
                    x.BlockedId,
                    Username = !string.IsNullOrWhiteSpace(x.Blocked.Username)
                        ? x.Blocked.Username
                        : x.BlockedSnapshotUsername ?? string.Empty,
                    x.Blocked.FullName,
                    x.Blocked.AvatarUrl,
                    x.CreatedAt
                });

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var words = keyword.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    var searchPattern = $"%{word}%";
                    query = query.Where(x =>
                        EF.Functions.ILike(AppDbContext.Unaccent(x.FullName ?? string.Empty), AppDbContext.Unaccent(searchPattern)) ||
                        EF.Functions.ILike(x.Username ?? string.Empty, searchPattern));
                }
            }

            var totalItems = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Username)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(x => new BlockedAccountListItemModel
                {
                    AccountId = x.BlockedId,
                    Username = x.Username ?? string.Empty,
                    FullName = x.FullName ?? string.Empty,
                    AvatarUrl = x.AvatarUrl,
                    BlockedAt = x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return (items, totalItems);
        }

        public async Task<List<AccountBlockRelationModel>> GetRelationsAsync(
            Guid currentId,
            IEnumerable<Guid> targetIds,
            CancellationToken cancellationToken = default)
        {
            var safeTargetIds = (targetIds ?? Enumerable.Empty<Guid>())
                .Where(x => x != Guid.Empty && x != currentId)
                .Distinct()
                .ToList();

            if (safeTargetIds.Count == 0)
            {
                return new List<AccountBlockRelationModel>();
            }

            return await _context.AccountBlocks
                .AsNoTracking()
                .Where(x =>
                    (x.BlockerId == currentId && safeTargetIds.Contains(x.BlockedId)) ||
                    (x.BlockedId == currentId && safeTargetIds.Contains(x.BlockerId)))
                .GroupBy(x => x.BlockerId == currentId ? x.BlockedId : x.BlockerId)
                .Select(g => new AccountBlockRelationModel
                {
                    TargetId = g.Key,
                    IsBlockedByCurrentUser = g.Any(x => x.BlockerId == currentId),
                    IsBlockedByTargetUser = g.Any(x => x.BlockedId == currentId)
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<AccountBlockPairRelationModel>> GetRelationPairsAsync(
            IEnumerable<Guid> currentIds,
            IEnumerable<Guid> targetIds,
            CancellationToken cancellationToken = default)
        {
            var safeCurrentIds = (currentIds ?? Enumerable.Empty<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();
            var safeTargetIds = (targetIds ?? Enumerable.Empty<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();

            if (safeCurrentIds.Count == 0 || safeTargetIds.Count == 0)
            {
                return new List<AccountBlockPairRelationModel>();
            }

            var blockedByCurrentQuery = _context.AccountBlocks
                .AsNoTracking()
                .Where(x =>
                    safeCurrentIds.Contains(x.BlockerId) &&
                    safeTargetIds.Contains(x.BlockedId))
                .Select(x => new
                {
                    CurrentId = x.BlockerId,
                    TargetId = x.BlockedId,
                    IsBlockedByCurrentUser = true,
                    IsBlockedByTargetUser = false
                });

            var blockedByTargetQuery = _context.AccountBlocks
                .AsNoTracking()
                .Where(x =>
                    safeCurrentIds.Contains(x.BlockedId) &&
                    safeTargetIds.Contains(x.BlockerId))
                .Select(x => new
                {
                    CurrentId = x.BlockedId,
                    TargetId = x.BlockerId,
                    IsBlockedByCurrentUser = false,
                    IsBlockedByTargetUser = true
                });

            return await blockedByCurrentQuery
                .Concat(blockedByTargetQuery)
                .GroupBy(x => new { x.CurrentId, x.TargetId })
                .Select(g => new AccountBlockPairRelationModel
                {
                    CurrentId = g.Key.CurrentId,
                    TargetId = g.Key.TargetId,
                    IsBlockedByCurrentUser = g.Any(x => x.IsBlockedByCurrentUser),
                    IsBlockedByTargetUser = g.Any(x => x.IsBlockedByTargetUser)
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> IsBlockedByCurrentUserAsync(Guid currentId, Guid targetId)
        {
            return await _context.AccountBlocks
                .AsNoTracking()
                .AnyAsync(x => x.BlockerId == currentId && x.BlockedId == targetId);
        }

        public async Task<bool> IsBlockedEitherWayAsync(Guid currentId, Guid targetId)
        {
            return await _context.AccountBlocks
                .AsNoTracking()
                .AnyAsync(x =>
                    (x.BlockerId == currentId && x.BlockedId == targetId) ||
                    (x.BlockerId == targetId && x.BlockedId == currentId));
        }

        public async Task<bool> HasAnyRelationWithinAsync(
            IEnumerable<Guid> accountIds,
            IEnumerable<Guid>? focusAccountIds = null,
            CancellationToken cancellationToken = default)
        {
            var safeAccountIds = (accountIds ?? Enumerable.Empty<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();

            if (safeAccountIds.Count < 2)
            {
                return false;
            }

            var safeFocusIds = (focusAccountIds ?? safeAccountIds)
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();

            if (safeFocusIds.Count == 0)
            {
                return false;
            }

            return await _context.AccountBlocks
                .AsNoTracking()
                .AnyAsync(x =>
                    safeAccountIds.Contains(x.BlockerId) &&
                    safeAccountIds.Contains(x.BlockedId) &&
                    (safeFocusIds.Contains(x.BlockerId) || safeFocusIds.Contains(x.BlockedId)),
                    cancellationToken);
        }

        public async Task<int> RemoveAsync(Guid blockerId, Guid blockedId)
        {
            return await _context.AccountBlocks
                .Where(x => x.BlockerId == blockerId && x.BlockedId == blockedId)
                .ExecuteDeleteAsync();
        }
    }
}
