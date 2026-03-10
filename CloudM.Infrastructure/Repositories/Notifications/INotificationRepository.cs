using CloudM.Domain.Entities;
using CloudM.Domain.Enums;

namespace CloudM.Infrastructure.Repositories.Notifications
{
    public interface INotificationRepository
    {
        Task<Notification?> GetByAggregateKeyAsync(
            Guid recipientId,
            NotificationTypeEnum type,
            string aggregateKey,
            bool includeContributions = false,
            CancellationToken cancellationToken = default);
        Task<Notification?> GetByIdAsync(
            Guid notificationId,
            bool includeContributions = false,
            CancellationToken cancellationToken = default);
        Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
        void Remove(Notification notification);
        Task<NotificationContribution?> GetContributionAsync(
            Guid notificationId,
            NotificationSourceTypeEnum sourceType,
            Guid sourceId,
            CancellationToken cancellationToken = default);
        Task AddContributionAsync(NotificationContribution contribution, CancellationToken cancellationToken = default);
        Task<List<NotificationContribution>> GetActiveContributionsAsync(Guid notificationId, CancellationToken cancellationToken = default);
        Task<(List<Notification> Items, DateTime? NextCursorLastEventAt, Guid? NextCursorNotificationId)> GetByCursorAsync(
            Guid recipientId,
            DateTime? lastNotificationsSeenAt,
            bool unreadOnly,
            int limit,
            DateTime? cursorLastEventAt,
            Guid? cursorNotificationId,
            CancellationToken cancellationToken = default);
        Task<int> GetUnreadCountAsync(
            Guid recipientId,
            DateTime? lastNotificationsSeenAt,
            CancellationToken cancellationToken = default);
        Task<List<Guid>> GetRecipientsByTargetAsync(
            NotificationTypeEnum type,
            NotificationTargetKindEnum targetKind,
            Guid targetId,
            CancellationToken cancellationToken = default);
        Task<List<Guid>> GetRecipientsByAggregateKeyAsync(
            NotificationTypeEnum type,
            string aggregateKey,
            CancellationToken cancellationToken = default);
        Task DeleteByRecipientAndAggregateKeysAsync(
            Guid recipientId,
            NotificationTypeEnum type,
            IEnumerable<string> aggregateKeys,
            CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
