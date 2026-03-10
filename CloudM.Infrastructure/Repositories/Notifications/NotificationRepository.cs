using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CloudM.Infrastructure.Repositories.Notifications
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly AppDbContext _context;

        public NotificationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Notification?> GetByAggregateKeyAsync(
            Guid recipientId,
            NotificationTypeEnum type,
            string aggregateKey,
            bool includeContributions = false,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Notifications
                .Where(x => x.RecipientId == recipientId && x.Type == type && x.AggregateKey == aggregateKey);

            if (includeContributions)
            {
                query = query
                    .Include(x => x.Contributions)
                        .ThenInclude(x => x.Actor);
            }

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Notification?> GetByIdAsync(
            Guid notificationId,
            bool includeContributions = false,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Notifications
                .Where(x => x.NotificationId == notificationId);

            if (includeContributions)
            {
                query = query
                    .Include(x => x.Contributions)
                        .ThenInclude(x => x.Actor);
            }

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            return _context.Notifications.AddAsync(notification, cancellationToken).AsTask();
        }

        public void Remove(Notification notification)
        {
            _context.Notifications.Remove(notification);
        }

        public async Task<NotificationContribution?> GetContributionAsync(
            Guid notificationId,
            NotificationSourceTypeEnum sourceType,
            Guid sourceId,
            CancellationToken cancellationToken = default)
        {
            return await _context.NotificationContributions
                .Include(x => x.Actor)
                .FirstOrDefaultAsync(
                    x => x.NotificationId == notificationId && x.SourceType == sourceType && x.SourceId == sourceId,
                    cancellationToken);
        }

        public Task AddContributionAsync(NotificationContribution contribution, CancellationToken cancellationToken = default)
        {
            return _context.NotificationContributions.AddAsync(contribution, cancellationToken).AsTask();
        }

        public async Task<List<NotificationContribution>> GetActiveContributionsAsync(Guid notificationId, CancellationToken cancellationToken = default)
        {
            return await _context.NotificationContributions
                .Include(x => x.Actor)
                .Where(x => x.NotificationId == notificationId && x.IsActive)
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<(List<Notification> Items, DateTime? NextCursorLastEventAt, Guid? NextCursorNotificationId)> GetByCursorAsync(
            Guid recipientId,
            DateTime? lastNotificationsSeenAt,
            bool unreadOnly,
            int limit,
            DateTime? cursorLastEventAt,
            Guid? cursorNotificationId,
            CancellationToken cancellationToken = default)
        {
            if (limit <= 0) limit = 20;
            if (limit > 50) limit = 50;

            var query = _context.Notifications
                .Where(x =>
                    x.RecipientId == recipientId &&
                    x.Type != NotificationTypeEnum.FollowRequest);

            if (unreadOnly && lastNotificationsSeenAt.HasValue)
            {
                query = query.Where(x =>
                    (x.Contributions.Any() &&
                     x.Contributions.Any(c =>
                         c.IsActive &&
                         c.UpdatedAt > lastNotificationsSeenAt.Value)) ||
                    (!x.Contributions.Any() &&
                     x.LastEventAt > lastNotificationsSeenAt.Value));
            }

            if (cursorLastEventAt.HasValue && cursorNotificationId.HasValue)
            {
                query = query.Where(x =>
                    x.LastEventAt < cursorLastEventAt.Value ||
                    (x.LastEventAt == cursorLastEventAt.Value && x.NotificationId.CompareTo(cursorNotificationId.Value) < 0));
            }

            var candidates = await query
                .OrderByDescending(x => x.LastEventAt)
                .ThenByDescending(x => x.NotificationId)
                .Take(limit + 1)
                .ToListAsync(cancellationToken);

            var hasMore = candidates.Count > limit;
            var items = hasMore ? candidates.Take(limit).ToList() : candidates;

            DateTime? nextCursorLastEventAt = null;
            Guid? nextCursorNotificationId = null;
            if (hasMore && items.Count > 0)
            {
                var last = items[^1];
                nextCursorLastEventAt = last.LastEventAt;
                nextCursorNotificationId = last.NotificationId;
            }

            return (items, nextCursorLastEventAt, nextCursorNotificationId);
        }

        public async Task<int> GetUnreadCountAsync(
            Guid recipientId,
            DateTime? lastNotificationsSeenAt,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Notifications
                .AsNoTracking()
                .Where(x =>
                    x.RecipientId == recipientId &&
                    x.Type != NotificationTypeEnum.FollowRequest);

            if (lastNotificationsSeenAt.HasValue)
            {
                query = query.Where(x =>
                    (x.Contributions.Any() &&
                     x.Contributions.Any(c =>
                         c.IsActive &&
                         c.UpdatedAt > lastNotificationsSeenAt.Value)) ||
                    (!x.Contributions.Any() &&
                     x.LastEventAt > lastNotificationsSeenAt.Value));
            }

            return await query.CountAsync(cancellationToken);
        }

        public async Task<List<Guid>> GetRecipientsByTargetAsync(
            NotificationTypeEnum type,
            NotificationTargetKindEnum targetKind,
            Guid targetId,
            CancellationToken cancellationToken = default)
        {
            if (targetId == Guid.Empty)
            {
                return new List<Guid>();
            }

            return await _context.Notifications
                .AsNoTracking()
                .Where(x => x.Type == type && x.TargetKind == targetKind && x.TargetId == targetId)
                .Select(x => x.RecipientId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Guid>> GetRecipientsByAggregateKeyAsync(
            NotificationTypeEnum type,
            string aggregateKey,
            CancellationToken cancellationToken = default)
        {
            var safeAggregateKey = (aggregateKey ?? string.Empty).Trim();
            if (safeAggregateKey.Length == 0)
            {
                return new List<Guid>();
            }

            return await _context.Notifications
                .AsNoTracking()
                .Where(x => x.Type == type && x.AggregateKey == safeAggregateKey)
                .Select(x => x.RecipientId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public async Task DeleteByRecipientAndAggregateKeysAsync(
            Guid recipientId,
            NotificationTypeEnum type,
            IEnumerable<string> aggregateKeys,
            CancellationToken cancellationToken = default)
        {
            var safeAggregateKeys = (aggregateKeys ?? Enumerable.Empty<string>())
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => x.Length > 0)
                .Distinct()
                .ToList();
            if (recipientId == Guid.Empty || safeAggregateKeys.Count == 0)
            {
                return;
            }

            await _context.Notifications
                .Where(x =>
                    x.RecipientId == recipientId &&
                    x.Type == type &&
                    safeAggregateKeys.Contains(x.AggregateKey))
                .ExecuteDeleteAsync(cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
