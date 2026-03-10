using CloudM.Application.Services.RealtimeServices;

namespace CloudM.Application.Services.NotificationServices
{
    public class NotificationDispatcher : INotificationDispatcher
    {
        private readonly IRealtimeService _realtimeService;

        public NotificationDispatcher(IRealtimeService realtimeService)
        {
            _realtimeService = realtimeService;
        }

        public async Task DispatchAsync(NotificationProjectionResult result, CancellationToken cancellationToken = default)
        {
            if (result.RecipientId == Guid.Empty || result.Action == NotificationProjectionActionEnum.None)
            {
                return;
            }

            if (result.Action == NotificationProjectionActionEnum.Upsert)
            {
                await _realtimeService.NotifyNotificationUpsertAsync(
                    result.RecipientId,
                    result.NotificationId,
                    result.EventId,
                    result.OccurredAt,
                    result.AffectsUnread);
                return;
            }

            if (result.Action == NotificationProjectionActionEnum.Remove && result.NotificationId.HasValue)
            {
                await _realtimeService.NotifyNotificationRemovedAsync(
                    result.RecipientId,
                    result.NotificationId.Value,
                    result.EventId,
                    result.OccurredAt);
            }
        }
    }
}
