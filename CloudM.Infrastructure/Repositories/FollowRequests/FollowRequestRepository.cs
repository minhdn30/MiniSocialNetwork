using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CloudM.Infrastructure.Repositories.FollowRequests
{
    public class FollowRequestRepository : IFollowRequestRepository
    {
        private readonly AppDbContext _context;

        public FollowRequestRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsFollowRequestExistAsync(Guid requesterId, Guid targetId)
        {
            return await _context.FollowRequests
                .AnyAsync(fr => fr.RequesterId == requesterId && fr.TargetId == targetId);
        }

        public Task AddFollowRequestAsync(FollowRequest followRequest)
        {
            _context.FollowRequests.Add(followRequest);
            return Task.CompletedTask;
        }

        public async Task<bool> AddFollowRequestIgnoreExistingAsync(FollowRequest followRequest, CancellationToken cancellationToken = default)
        {
            if (followRequest == null ||
                followRequest.RequesterId == Guid.Empty ||
                followRequest.TargetId == Guid.Empty ||
                followRequest.RequesterId == followRequest.TargetId)
            {
                return false;
            }

            var requesterIdParam = new NpgsqlParameter<Guid>("p_requester_id", followRequest.RequesterId);
            var targetIdParam = new NpgsqlParameter<Guid>("p_target_id", followRequest.TargetId);
            var createdAtParam = new NpgsqlParameter<DateTime>("p_created_at", NpgsqlDbType.TimestampTz)
            {
                TypedValue = DateTime.SpecifyKind(
                    followRequest.CreatedAt == default ? DateTime.UtcNow : followRequest.CreatedAt,
                    DateTimeKind.Utc)
            };

            var affected = await _context.Database.ExecuteSqlRawAsync(@"
INSERT INTO ""FollowRequests"" (""RequesterId"", ""TargetId"", ""CreatedAt"")
VALUES (@p_requester_id, @p_target_id, @p_created_at)
ON CONFLICT (""RequesterId"", ""TargetId"") DO NOTHING;",
                new object[] { requesterIdParam, targetIdParam, createdAtParam },
                cancellationToken);

            return affected > 0;
        }

        public async Task<int> RemoveFollowRequestAsync(Guid requesterId, Guid targetId)
        {
            return await _context.FollowRequests
                .Where(fr => fr.RequesterId == requesterId && fr.TargetId == targetId)
                .ExecuteDeleteAsync();
        }

        public async Task<(List<PendingFollowRequestListItem> Items, DateTime? NextCursorCreatedAt, Guid? NextCursorRequesterId)> GetPendingByTargetAsync(
            Guid targetId,
            int limit,
            DateTime? cursorCreatedAt,
            Guid? cursorRequesterId,
            CancellationToken cancellationToken = default)
        {
            var safeLimit = limit <= 0 ? 20 : Math.Min(limit, 50);

            var query = _context.FollowRequests
                .AsNoTracking()
                .Where(x =>
                    x.TargetId == targetId &&
                    x.Requester.Status == AccountStatusEnum.Active);

            if (cursorCreatedAt.HasValue && cursorRequesterId.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedAt < cursorCreatedAt.Value ||
                    (x.CreatedAt == cursorCreatedAt.Value && x.RequesterId.CompareTo(cursorRequesterId.Value) < 0));
            }

            var candidates = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.RequesterId)
                .Select(x => new PendingFollowRequestListItem
                {
                    RequesterId = x.RequesterId,
                    Username = x.Requester.Username,
                    FullName = x.Requester.FullName,
                    AvatarUrl = x.Requester.AvatarUrl,
                    CreatedAt = x.CreatedAt
                })
                .Take(safeLimit + 1)
                .ToListAsync(cancellationToken);

            var hasMore = candidates.Count > safeLimit;
            var items = hasMore ? candidates.Take(safeLimit).ToList() : candidates;

            DateTime? nextCursorCreatedAt = null;
            Guid? nextCursorRequesterId = null;
            if (hasMore && items.Count > 0)
            {
                var last = items[^1];
                nextCursorCreatedAt = last.CreatedAt;
                nextCursorRequesterId = last.RequesterId;
            }

            return (items, nextCursorCreatedAt, nextCursorRequesterId);
        }

        public async Task<int> GetPendingCountByTargetAsync(Guid targetId, CancellationToken cancellationToken = default)
        {
            return await _context.FollowRequests
                .AsNoTracking()
                .Where(x =>
                    x.TargetId == targetId &&
                    x.Requester.Status == AccountStatusEnum.Active)
                .CountAsync(cancellationToken);
        }

        public async Task<List<ClaimedAutoAcceptFollowRequest>> ClaimAutoAcceptBatchAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            var safeBatchSize = Math.Max(1, batchSize);
            var sql = $@"
WITH picked AS (
    SELECT
        fr.""RequesterId"",
        fr.""TargetId"",
        requester.""Status"" AS ""RequesterStatus""
    FROM ""FollowRequests"" fr
    INNER JOIN ""AccountSettings"" settings ON settings.""AccountId"" = fr.""TargetId""
    INNER JOIN ""Accounts"" requester ON requester.""AccountId"" = fr.""RequesterId""
    INNER JOIN ""Accounts"" target ON target.""AccountId"" = fr.""TargetId""
    WHERE settings.""FollowPrivacy"" = {(int)FollowPrivacyEnum.Anyone}
      AND target.""Status"" = {(int)AccountStatusEnum.Active}
    ORDER BY fr.""CreatedAt"", fr.""TargetId"", fr.""RequesterId""
    LIMIT {safeBatchSize}
    FOR UPDATE OF fr SKIP LOCKED
)
DELETE FROM ""FollowRequests"" fr
USING picked
WHERE fr.""RequesterId"" = picked.""RequesterId""
  AND fr.""TargetId"" = picked.""TargetId""
RETURNING
    picked.""RequesterId"",
    picked.""TargetId"",
    picked.""RequesterStatus"";";

            return await _context.Database
                .SqlQueryRaw<ClaimedAutoAcceptFollowRequest>(sql)
                .ToListAsync(cancellationToken);
        }
    }
}
