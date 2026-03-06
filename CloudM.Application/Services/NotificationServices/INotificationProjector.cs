using CloudM.Domain.Entities;

namespace CloudM.Application.Services.NotificationServices
{
    public interface INotificationProjector
    {
        Task<NotificationProjectionResult> ProjectAsync(NotificationOutbox outbox, CancellationToken cancellationToken = default);
    }
}
