using CloudM.Application.DTOs.NotificationDTOs;
using CloudM.Domain.Enums;

namespace CloudM.Application.Services.NotificationServices
{
    public interface INotificationService
    {
        Task EnqueueAggregateEventAsync(NotificationAggregateEvent request, CancellationToken cancellationToken = default);
        Task EnqueueTargetUnavailableEventAsync(NotificationTargetUnavailableEvent request, CancellationToken cancellationToken = default);
        Task EnqueueTargetUnavailableForExistingRecipientsAsync(
            NotificationTypeEnum type,
            NotificationTargetKindEnum targetKind,
            Guid targetId,
            Guid initiatorId,
            DateTime? occurredAt = null,
            CancellationToken cancellationToken = default);
        Task<NotificationCursorResponse> GetNotificationsAsync(
            Guid recipientId,
            NotificationCursorRequest request,
            CancellationToken cancellationToken = default);
        Task<NotificationUnreadSummaryResponse> GetUnreadSummaryAsync(
            Guid recipientId,
            CancellationToken cancellationToken = default);
        Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken = default);
        Task<NotificationUnreadSummaryResponse> UpdateReadStateAsync(
            Guid recipientId,
            NotificationReadStateRequest request,
            CancellationToken cancellationToken = default);
    }
}
