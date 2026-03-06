using CloudM.Application.Services.NotificationServices;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CloudM.API.Services
{
    public class NotificationOutboxWorkerHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly NotificationOptions _options;
        private readonly ILogger<NotificationOutboxWorkerHostedService> _logger;
        private DateTime _lastCleanupAt = DateTime.MinValue;

        public NotificationOutboxWorkerHostedService(
            IServiceScopeFactory scopeFactory,
            IOptions<NotificationOptions> options,
            ILogger<NotificationOutboxWorkerHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options?.Value ?? new NotificationOptions();
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_options.EnableWorker)
                {
                    await DelayAsync(2000, stoppingToken);
                    continue;
                }

                try
                {
                    await ProcessBatchAsync(stoppingToken);
                    await TryCleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Notification outbox worker iteration failed.");
                }

                await DelayAsync(_options.OutboxPollIntervalMs, stoppingToken);
            }
        }

        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            var utcNow = DateTime.UtcNow;
            var batchSize = Math.Max(1, _options.OutboxBatchSize);
            var lockSeconds = Math.Max(5, _options.OutboxLockSeconds);
            using var scope = _scopeFactory.CreateScope();
            var notificationOutboxRepository = scope.ServiceProvider.GetRequiredService<INotificationOutboxRepository>();
            var notificationProjector = scope.ServiceProvider.GetRequiredService<INotificationProjector>();
            var notificationDispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

            var outboxes = await notificationOutboxRepository.ClaimBatchAsync(
                batchSize,
                utcNow,
                lockSeconds,
                cancellationToken);
            if (outboxes.Count == 0)
            {
                return;
            }

            foreach (var outbox in outboxes)
            {
                try
                {
                    if (TryGetAggregateOrderingKey(outbox, out var type, out var aggregateKey))
                    {
                        var hasOlderPending = await notificationOutboxRepository.HasOlderAggregateEventPendingAsync(
                            outbox.RecipientId,
                            type,
                            aggregateKey,
                            outbox.OccurredAt,
                            outbox.OutboxId,
                            cancellationToken);
                        if (hasOlderPending)
                        {
                            await notificationOutboxRepository.DeferAsync(
                                outbox.OutboxId,
                                DateTime.UtcNow.AddMilliseconds(200),
                                cancellationToken);
                            continue;
                        }
                    }

                    var projectionResult = await notificationProjector.ProjectAsync(outbox, cancellationToken);

                    try
                    {
                        await notificationDispatcher.DispatchAsync(projectionResult, cancellationToken);
                    }
                    catch (Exception dispatchEx)
                    {
                        _logger.LogWarning(dispatchEx, "Notification realtime dispatch failed for outbox {OutboxId}.", outbox.OutboxId);
                    }

                    await notificationOutboxRepository.MarkStatusAsync(
                        outbox.OutboxId,
                        NotificationOutboxStatusEnum.Processed,
                        nextRetryAt: DateTime.UtcNow,
                        processedAt: DateTime.UtcNow,
                        lockedUntil: null,
                        lastError: null,
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    var isDeadLetter = outbox.AttemptCount >= Math.Max(1, _options.MaxRetryAttempts);
                    var nextRetryAt = isDeadLetter
                        ? DateTime.UtcNow
                        : DateTime.UtcNow.AddMilliseconds(CalculateBackoffDelayMs(outbox.AttemptCount));

                    await notificationOutboxRepository.MarkStatusAsync(
                        outbox.OutboxId,
                        isDeadLetter
                            ? NotificationOutboxStatusEnum.DeadLetter
                            : NotificationOutboxStatusEnum.Pending,
                        nextRetryAt: nextRetryAt,
                        processedAt: null,
                        lockedUntil: null,
                        lastError: TruncateError(ex.Message, 1900),
                        cancellationToken: cancellationToken);
                }
            }
        }

        private async Task TryCleanupAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            if (_lastCleanupAt != DateTime.MinValue && (now - _lastCleanupAt).TotalMinutes < 30)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var notificationOutboxRepository = scope.ServiceProvider.GetRequiredService<INotificationOutboxRepository>();
            var retentionDays = Math.Max(1, _options.RetentionDays);
            var cutoff = now.AddDays(-retentionDays);
            var removed = await notificationOutboxRepository.CleanupProcessedAsync(cutoff, cancellationToken);
            _lastCleanupAt = now;
            if (removed > 0)
            {
                _logger.LogInformation("Notification outbox cleanup removed {Count} retained records.", removed);
            }
        }

        private int CalculateBackoffDelayMs(int attemptCount)
        {
            var baseDelay = Math.Max(100, _options.RetryBaseDelayMs);
            var maxDelay = Math.Max(baseDelay, _options.RetryMaxDelayMs);
            var exponent = Math.Max(0, attemptCount - 1);
            var factor = Math.Pow(2, Math.Min(exponent, 10));
            var delay = (int)Math.Min(baseDelay * factor, maxDelay);
            return delay;
        }

        private static string? TruncateError(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var safeValue = value.Trim();
            return safeValue.Length <= maxLength
                ? safeValue
                : safeValue.Substring(0, maxLength);
        }

        private static async Task DelayAsync(int delayMs, CancellationToken cancellationToken)
        {
            var safeDelayMs = Math.Max(100, delayMs);
            await Task.Delay(safeDelayMs, cancellationToken);
        }

        private static bool TryGetAggregateOrderingKey(
            CloudM.Domain.Entities.NotificationOutbox outbox,
            out NotificationTypeEnum type,
            out string aggregateKey)
        {
            type = default;
            aggregateKey = string.Empty;

            if (!string.Equals(outbox.EventType, NotificationOutboxEventTypes.AggregateChanged, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(outbox.PayloadJson))
            {
                return false;
            }

            try
            {
                var payload = JsonSerializer.Deserialize<NotificationAggregateChangedPayload>(outbox.PayloadJson);
                if (payload == null || string.IsNullOrWhiteSpace(payload.AggregateKey))
                {
                    return false;
                }

                type = payload.Type;
                aggregateKey = payload.AggregateKey.Trim();
                return aggregateKey.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
