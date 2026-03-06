using CloudM.Application.DTOs.NotificationDTOs;
using CloudM.Domain.Enums;

namespace CloudM.Application.Services.NotificationServices
{
    public sealed class NullNotificationService : INotificationService
    {
        public static readonly NullNotificationService Instance = new();

        private NullNotificationService()
        {
        }

        public Task EnqueueAggregateEventAsync(NotificationAggregateEvent request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task EnqueueTargetUnavailableEventAsync(NotificationTargetUnavailableEvent request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task EnqueueTargetUnavailableForExistingRecipientsAsync(
            NotificationTypeEnum type,
            NotificationTargetKindEnum targetKind,
            Guid targetId,
            Guid initiatorId,
            DateTime? occurredAt = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<NotificationCursorResponse> GetNotificationsAsync(
            Guid recipientId,
            NotificationCursorRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NotificationCursorResponse());
        }

        public Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }
}
