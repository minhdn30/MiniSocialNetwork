using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CloudM.Infrastructure.Repositories.Notifications
{
    public class NotificationOutboxRepository : INotificationOutboxRepository
    {
        private const string AggregateChangedEventType = "notification.aggregate.changed";
        private readonly AppDbContext _context;

        public NotificationOutboxRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task AddAsync(NotificationOutbox outbox, CancellationToken cancellationToken = default)
        {
            return _context.NotificationOutboxes.AddAsync(outbox, cancellationToken).AsTask();
        }

        public async Task AddRangeAsync(IEnumerable<NotificationOutbox> outboxes, CancellationToken cancellationToken = default)
        {
            var safeOutboxes = (outboxes ?? Enumerable.Empty<NotificationOutbox>()).ToList();
            if (safeOutboxes.Count == 0)
            {
                return;
            }

            await _context.NotificationOutboxes.AddRangeAsync(safeOutboxes, cancellationToken);
        }

        public async Task<List<NotificationOutbox>> ClaimBatchAsync(
            int batchSize,
            DateTime utcNow,
            int lockSeconds,
            CancellationToken cancellationToken = default)
        {
            if (batchSize <= 0)
            {
                return new List<NotificationOutbox>();
            }

            var lockUntil = utcNow.AddSeconds(Math.Max(lockSeconds, 5));

            return await _context.NotificationOutboxes
                .FromSqlInterpolated($@"
WITH picked AS (
    SELECT ""OutboxId""
    FROM ""NotificationOutboxes""
    WHERE (""Status"" = {(int)NotificationOutboxStatusEnum.Pending} OR ""Status"" = {(int)NotificationOutboxStatusEnum.Processing})
      AND ""NextRetryAt"" <= {utcNow}
      AND (""LockedUntil"" IS NULL OR ""LockedUntil"" <= {utcNow})
    ORDER BY ""OccurredAt"", ""OutboxId""
    LIMIT {batchSize}
    FOR UPDATE SKIP LOCKED
)
UPDATE ""NotificationOutboxes"" AS outbox
SET ""Status"" = {(int)NotificationOutboxStatusEnum.Processing},
    ""AttemptCount"" = outbox.""AttemptCount"" + 1,
    ""LockedUntil"" = {lockUntil}
FROM picked
WHERE outbox.""OutboxId"" = picked.""OutboxId""
RETURNING outbox.*")
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasOlderAggregateEventPendingAsync(
            Guid recipientId,
            NotificationTypeEnum type,
            string aggregateKey,
            DateTime occurredAt,
            Guid currentOutboxId,
            CancellationToken cancellationToken = default)
        {
            var safeAggregateKey = (aggregateKey ?? string.Empty).Trim();
            if (recipientId == Guid.Empty || string.IsNullOrWhiteSpace(safeAggregateKey))
            {
                return false;
            }

            var currentOutboxIdText = currentOutboxId.ToString();
            return await _context.Database
                .SqlQuery<bool>($@"
SELECT EXISTS (
    SELECT 1
    FROM ""NotificationOutboxes""
    WHERE ""EventType"" = {AggregateChangedEventType}
      AND ""RecipientId"" = {recipientId}
      AND (""Status"" = {(int)NotificationOutboxStatusEnum.Pending} OR ""Status"" = {(int)NotificationOutboxStatusEnum.Processing})
      AND (""PayloadJson""::jsonb ->> 'Type')::int = {(int)type}
      AND (""PayloadJson""::jsonb ->> 'AggregateKey') = {safeAggregateKey}
      AND ""OutboxId"" <> {currentOutboxId}
      AND (
          ""OccurredAt"" < {occurredAt}
          OR (""OccurredAt"" = {occurredAt} AND ""OutboxId""::text < {currentOutboxIdText})
      )
) AS ""Value""")
                .SingleAsync(cancellationToken);
        }

        public Task DeferAsync(
            Guid outboxId,
            DateTime nextRetryAt,
            CancellationToken cancellationToken = default)
        {
            return _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""NotificationOutboxes""
SET ""Status"" = {(int)NotificationOutboxStatusEnum.Pending},
    ""LockedUntil"" = NULL,
    ""NextRetryAt"" = {nextRetryAt},
    ""AttemptCount"" = GREATEST(""AttemptCount"" - 1, 0)
WHERE ""OutboxId"" = {outboxId}", cancellationToken);
        }

        public async Task MarkStatusAsync(
            Guid outboxId,
            NotificationOutboxStatusEnum status,
            DateTime? nextRetryAt,
            DateTime? processedAt,
            DateTime? lockedUntil,
            string? lastError,
            CancellationToken cancellationToken = default)
        {
            var query = _context.NotificationOutboxes
                .Where(x => x.OutboxId == outboxId);

            if (nextRetryAt.HasValue)
            {
                await query.ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, status)
                    .SetProperty(x => x.NextRetryAt, nextRetryAt.Value)
                    .SetProperty(x => x.ProcessedAt, processedAt)
                    .SetProperty(x => x.LockedUntil, lockedUntil)
                    .SetProperty(x => x.LastError, lastError),
                    cancellationToken);
                return;
            }

            await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.ProcessedAt, processedAt)
                .SetProperty(x => x.LockedUntil, lockedUntil)
                .SetProperty(x => x.LastError, lastError),
                cancellationToken);
        }

        public Task<int> CleanupProcessedAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
        {
            return _context.NotificationOutboxes
                .Where(x =>
                    (x.Status == NotificationOutboxStatusEnum.Processed &&
                     x.ProcessedAt.HasValue &&
                     x.ProcessedAt.Value < cutoffUtc) ||
                    (x.Status == NotificationOutboxStatusEnum.DeadLetter &&
                     x.OccurredAt < cutoffUtc))
                .ExecuteDeleteAsync(cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
