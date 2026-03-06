namespace CloudM.Application.Services.NotificationServices
{
    public interface INotificationDispatcher
    {
        Task DispatchAsync(NotificationProjectionResult result, CancellationToken cancellationToken = default);
    }
}
