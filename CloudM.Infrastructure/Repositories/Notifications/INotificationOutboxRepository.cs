using CloudM.Domain.Entities;
using CloudM.Domain.Enums;

namespace CloudM.Infrastructure.Repositories.Notifications
{
    public interface INotificationOutboxRepository
    {
        Task AddAsync(NotificationOutbox outbox, CancellationToken cancellationToken = default);
        Task AddRangeAsync(IEnumerable<NotificationOutbox> outboxes, CancellationToken cancellationToken = default);
        Task<List<NotificationOutbox>> ClaimBatchAsync(
            int batchSize,
            DateTime utcNow,
            int lockSeconds,
            CancellationToken cancellationToken = default);
        Task<bool> HasOlderAggregateEventPendingAsync(
            Guid recipientId,
            NotificationTypeEnum type,
            string aggregateKey,
            DateTime occurredAt,
            Guid currentOutboxId,
            CancellationToken cancellationToken = default);
        Task DeferAsync(
            Guid outboxId,
            DateTime nextRetryAt,
            CancellationToken cancellationToken = default);
        Task MarkStatusAsync(
            Guid outboxId,
            NotificationOutboxStatusEnum status,
            DateTime? nextRetryAt,
            DateTime? processedAt,
            DateTime? lockedUntil,
            string? lastError,
            CancellationToken cancellationToken = default);
        Task<int> CleanupProcessedAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
