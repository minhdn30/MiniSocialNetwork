using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Repositories.Notifications;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CloudM.Application.Services.NotificationServices
{
    public class NotificationProjector : INotificationProjector
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly AppDbContext _context;

        public NotificationProjector(
            INotificationRepository notificationRepository,
            AppDbContext context)
        {
            _notificationRepository = notificationRepository;
            _context = context;
        }

        public async Task<NotificationProjectionResult> ProjectAsync(NotificationOutbox outbox, CancellationToken cancellationToken = default)
        {
            if (string.Equals(outbox.EventType, NotificationOutboxEventTypes.AggregateChanged, StringComparison.OrdinalIgnoreCase))
            {
                var payload = DeserializePayload<NotificationAggregateChangedPayload>(outbox.PayloadJson);
                if (payload == null)
                {
                    return AttachDispatchMetadata(None(outbox.RecipientId), outbox);
                }

                var result = await ProjectAggregateChangedAsync(outbox.RecipientId, payload, cancellationToken);
                return AttachDispatchMetadata(result, outbox);
            }

            if (string.Equals(outbox.EventType, NotificationOutboxEventTypes.TargetUnavailable, StringComparison.OrdinalIgnoreCase))
            {
                var payload = DeserializePayload<NotificationTargetUnavailablePayload>(outbox.PayloadJson);
                if (payload == null)
                {
                    return AttachDispatchMetadata(None(outbox.RecipientId), outbox);
                }

                var result = await ProjectTargetUnavailableAsync(outbox.RecipientId, payload, cancellationToken);
                return AttachDispatchMetadata(result, outbox);
            }

            if (string.Equals(outbox.EventType, NotificationOutboxEventTypes.TargetUnavailableBroadcast, StringComparison.OrdinalIgnoreCase))
            {
                var payload = DeserializePayload<NotificationTargetUnavailablePayload>(outbox.PayloadJson);
                if (payload == null)
                {
                    return AttachDispatchMetadata(None(outbox.RecipientId), outbox);
                }

                var result = await ProjectTargetUnavailableBroadcastAsync(payload, cancellationToken);
                return AttachDispatchMetadata(result, outbox);
            }

            return AttachDispatchMetadata(None(outbox.RecipientId), outbox);
        }

        private async Task<NotificationProjectionResult> ProjectAggregateChangedAsync(
            Guid recipientId,
            NotificationAggregateChangedPayload payload,
            CancellationToken cancellationToken)
        {
            if (recipientId == Guid.Empty ||
                string.IsNullOrWhiteSpace(payload.AggregateKey))
            {
                return None(recipientId);
            }

            var notification = await _notificationRepository.GetByAggregateKeyAsync(
                recipientId,
                payload.Type,
                payload.AggregateKey,
                includeContributions: true,
                cancellationToken: cancellationToken);
            var previousSeenStateTimestamp = ResolveProjectionSeenStateTimestamp(notification);

            if (payload.Action == NotificationAggregateActionEnum.Upsert && (!payload.ActorId.HasValue || payload.ActorId.Value == Guid.Empty))
            {
                return None(recipientId);
            }

            if (notification == null)
            {
                if (payload.Action != NotificationAggregateActionEnum.Upsert)
                {
                    return None(recipientId);
                }

                notification = new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    RecipientId = recipientId,
                    Type = payload.Type,
                    AggregateKey = payload.AggregateKey,
                    TargetKind = payload.TargetKind,
                    TargetId = payload.TargetId,
                    CreatedAt = payload.OccurredAt,
                    LastEventAt = payload.OccurredAt,
                    UpdatedAt = DateTime.UtcNow,
                    State = NotificationStateEnum.Active
                };

                await _notificationRepository.AddAsync(notification, cancellationToken);
            }
            else
            {
                notification.TargetKind = payload.TargetKind;
                notification.TargetId = payload.TargetId;
                if (payload.Action == NotificationAggregateActionEnum.Upsert)
                {
                    notification.LastEventAt = notification.LastEventAt < payload.OccurredAt
                        ? payload.OccurredAt
                        : notification.LastEventAt;
                }
                notification.UpdatedAt = DateTime.UtcNow;
            }

            if (payload.Action == NotificationAggregateActionEnum.Upsert)
            {
                var contribution = notification.Contributions
                    .FirstOrDefault(x => x.SourceType == payload.SourceType && x.SourceId == payload.SourceId);

                if (contribution == null)
                {
                    contribution = new NotificationContribution
                    {
                        ContributionId = Guid.NewGuid(),
                        NotificationId = notification.NotificationId,
                        SourceType = payload.SourceType,
                        SourceId = payload.SourceId,
                        ActorId = payload.ActorId!.Value,
                        IsActive = true,
                        CreatedAt = payload.OccurredAt,
                        UpdatedAt = payload.OccurredAt
                    };
                    await _notificationRepository.AddContributionAsync(contribution, cancellationToken);
                    notification.Contributions.Add(contribution);
                }
                else
                {
                    contribution.ActorId = payload.ActorId!.Value;
                    contribution.IsActive = true;
                    contribution.UpdatedAt = payload.OccurredAt;
                }

            }
            else if (payload.Action == NotificationAggregateActionEnum.DeactivateAll)
            {
                foreach (var contribution in notification.Contributions.Where(x => x.IsActive))
                {
                    contribution.IsActive = false;
                    contribution.UpdatedAt = payload.OccurredAt;
                }
            }
            else
            {
                var contribution = notification.Contributions
                    .FirstOrDefault(x => x.SourceType == payload.SourceType && x.SourceId == payload.SourceId);

                if (contribution != null)
                {
                    contribution.IsActive = false;
                    contribution.UpdatedAt = payload.OccurredAt;
                }
            }

            await _notificationRepository.SaveChangesAsync(cancellationToken);

            var recomputedNotification = await _notificationRepository.GetByIdAsync(
                notification.NotificationId,
                includeContributions: true,
                cancellationToken: cancellationToken);
            if (recomputedNotification == null)
            {
                return None(recipientId);
            }

            var activeContributions = recomputedNotification.Contributions
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.UpdatedAt)
                .ToList();

            if (activeContributions.Count == 0)
            {
                if (payload.KeepWhenEmpty)
                {
                    recomputedNotification.State = NotificationStateEnum.Unavailable;
                    recomputedNotification.ActorCount = 0;
                    recomputedNotification.EventCount = 0;
                    recomputedNotification.LastActorId = null;
                    recomputedNotification.LastActorSnapshot = null;
                    recomputedNotification.UpdatedAt = DateTime.UtcNow;
                    await _notificationRepository.SaveChangesAsync(cancellationToken);
                    return Upsert(recipientId, recomputedNotification.NotificationId, false);
                }

                var removedId = recomputedNotification.NotificationId;
                _notificationRepository.Remove(recomputedNotification);
                await _notificationRepository.SaveChangesAsync(cancellationToken);
                return Remove(recipientId, removedId);
            }

            var activeActorContributions = activeContributions
                .Where(x => x.Actor != null && x.Actor.Status == AccountStatusEnum.Active)
                .ToList();

            if (activeActorContributions.Count == 0)
            {
                recomputedNotification.State = NotificationStateEnum.Unavailable;
                recomputedNotification.ActorCount = 0;
                recomputedNotification.EventCount = activeContributions.Count;
                recomputedNotification.LastActorId = null;
                recomputedNotification.LastActorSnapshot = null;
                recomputedNotification.LastEventAt = activeContributions[0].UpdatedAt;
                recomputedNotification.UpdatedAt = DateTime.UtcNow;
                await _notificationRepository.SaveChangesAsync(cancellationToken);
                var nextSeenStateTimestamp = ResolveProjectionSeenStateTimestamp(recomputedNotification, activeContributions);
                var affectsUnread = ShouldAffectUnread(
                    payload.Action,
                    previousSeenStateTimestamp,
                    nextSeenStateTimestamp);
                return Upsert(recipientId, recomputedNotification.NotificationId, affectsUnread);
            }

            var latestContribution = activeActorContributions[0];
            recomputedNotification.ActorCount = activeActorContributions
                .Select(x => x.ActorId)
                .Distinct()
                .Count();
            recomputedNotification.EventCount = activeContributions.Count;
            recomputedNotification.LastActorId = latestContribution.ActorId;
            recomputedNotification.LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
            {
                AccountId = latestContribution.ActorId,
                Username = latestContribution.Actor.Username,
                FullName = latestContribution.Actor.FullName,
                AvatarUrl = latestContribution.Actor.AvatarUrl
            });
            recomputedNotification.LastEventAt = latestContribution.UpdatedAt;
            recomputedNotification.State = await IsTargetAvailableAsync(
                    recomputedNotification.RecipientId,
                    recomputedNotification.TargetKind,
                    recomputedNotification.TargetId,
                    cancellationToken)
                ? NotificationStateEnum.Active
                : NotificationStateEnum.Unavailable;
            recomputedNotification.UpdatedAt = DateTime.UtcNow;

            await _notificationRepository.SaveChangesAsync(cancellationToken);
            var recomputedSeenStateTimestamp = ResolveProjectionSeenStateTimestamp(recomputedNotification, activeContributions);
            var upsertAffectsUnread = ShouldAffectUnread(
                payload.Action,
                previousSeenStateTimestamp,
                recomputedSeenStateTimestamp);
            return Upsert(recipientId, recomputedNotification.NotificationId, upsertAffectsUnread);
        }

        private async Task<NotificationProjectionResult> ProjectTargetUnavailableAsync(
            Guid recipientId,
            NotificationTargetUnavailablePayload payload,
            CancellationToken cancellationToken)
        {
            if (recipientId == Guid.Empty || payload.TargetId == Guid.Empty)
            {
                return None(recipientId);
            }

            var query = _context.Notifications
                .Where(x =>
                    x.RecipientId == recipientId &&
                    x.Type == payload.Type &&
                    x.TargetKind == payload.TargetKind &&
                    x.TargetId == payload.TargetId);

            if (!string.IsNullOrWhiteSpace(payload.AggregateKey))
            {
                query = query.Where(x => x.AggregateKey == payload.AggregateKey);
            }

            var notifications = await query.ToListAsync(cancellationToken);
            if (notifications.Count == 0)
            {
                return None(recipientId);
            }

            foreach (var notification in notifications)
            {
                notification.State = NotificationStateEnum.Unavailable;
                notification.UpdatedAt = DateTime.UtcNow;
            }

            await _notificationRepository.SaveChangesAsync(cancellationToken);
            return Upsert(recipientId, null, false);
        }

        private async Task<NotificationProjectionResult> ProjectTargetUnavailableBroadcastAsync(
            NotificationTargetUnavailablePayload payload,
            CancellationToken cancellationToken)
        {
            if (payload.TargetId == Guid.Empty)
            {
                return None(Guid.Empty);
            }

            var recipients = await _notificationRepository.GetRecipientsByTargetAsync(
                payload.Type,
                payload.TargetKind,
                payload.TargetId,
                cancellationToken);
            if (recipients.Count == 0)
            {
                return None(Guid.Empty);
            }

            var serializedPayload = JsonSerializer.Serialize(new NotificationTargetUnavailablePayload
            {
                Type = payload.Type,
                AggregateKey = null,
                TargetKind = payload.TargetKind,
                TargetId = payload.TargetId,
                OccurredAt = payload.OccurredAt == default ? DateTime.UtcNow : payload.OccurredAt
            });

            var outboxes = recipients
                .Where(x => x != Guid.Empty)
                .Distinct()
                .Select(recipientId => new NotificationOutbox
                {
                    OutboxId = Guid.NewGuid(),
                    EventType = NotificationOutboxEventTypes.TargetUnavailable,
                    RecipientId = recipientId,
                    PayloadJson = serializedPayload,
                    OccurredAt = payload.OccurredAt == default ? DateTime.UtcNow : payload.OccurredAt,
                    Status = NotificationOutboxStatusEnum.Pending,
                    AttemptCount = 0,
                    LockedUntil = null,
                    NextRetryAt = payload.OccurredAt == default ? DateTime.UtcNow : payload.OccurredAt
                })
                .ToList();

            if (outboxes.Count == 0)
            {
                return None(Guid.Empty);
            }

            await _context.NotificationOutboxes.AddRangeAsync(outboxes, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return None(Guid.Empty);
        }

        private async Task<bool> IsTargetAvailableAsync(
            Guid recipientId,
            NotificationTargetKindEnum targetKind,
            Guid? targetId,
            CancellationToken cancellationToken)
        {
            if (!targetId.HasValue || targetId.Value == Guid.Empty || targetKind == NotificationTargetKindEnum.None)
            {
                return true;
            }

            if (targetKind == NotificationTargetKindEnum.Account)
            {
                return await _context.Accounts
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.AccountId == targetId.Value && x.Status == AccountStatusEnum.Active,
                        cancellationToken);
            }

            if (targetKind == NotificationTargetKindEnum.Post)
            {
                var post = await _context.Posts
                    .AsNoTracking()
                    .Where(x => x.PostId == targetId.Value)
                    .Select(x => new
                    {
                        x.PostId,
                        x.AccountId,
                        x.Privacy,
                        x.IsDeleted,
                        OwnerStatus = x.Account.Status
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (post == null || post.IsDeleted || post.OwnerStatus != AccountStatusEnum.Active)
                {
                    return false;
                }

                if (post.AccountId == recipientId)
                {
                    return true;
                }

                if (post.Privacy == PostPrivacyEnum.Public)
                {
                    return true;
                }

                if (post.Privacy == PostPrivacyEnum.FollowOnly)
                {
                    return await _context.Follows
                        .AsNoTracking()
                        .AnyAsync(
                            x => x.FollowerId == recipientId && x.FollowedId == post.AccountId,
                            cancellationToken);
                }

                return false;
            }

            if (targetKind == NotificationTargetKindEnum.Story)
            {
                var nowUtc = DateTime.UtcNow;
                var story = await _context.Stories
                    .AsNoTracking()
                    .Where(x => x.StoryId == targetId.Value)
                    .Select(x => new
                    {
                        x.StoryId,
                        x.AccountId,
                        x.Privacy,
                        x.IsDeleted,
                        x.ExpiresAt,
                        OwnerStatus = x.Account.Status
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (story == null ||
                    story.IsDeleted ||
                    story.ExpiresAt <= nowUtc ||
                    story.OwnerStatus != AccountStatusEnum.Active)
                {
                    return false;
                }

                if (story.AccountId == recipientId)
                {
                    return true;
                }

                if (story.Privacy == StoryPrivacyEnum.Public)
                {
                    return true;
                }

                if (story.Privacy != StoryPrivacyEnum.FollowOnly)
                {
                    return false;
                }

                return await _context.Follows
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.FollowerId == recipientId && x.FollowedId == story.AccountId,
                        cancellationToken);
            }

            return false;
        }

        private static TPayload? DeserializePayload<TPayload>(string? payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<TPayload>(payloadJson);
            }
            catch
            {
                return default;
            }
        }

        private static NotificationProjectionResult None(Guid recipientId)
        {
            return new NotificationProjectionResult
            {
                Action = NotificationProjectionActionEnum.None,
                RecipientId = recipientId,
                NotificationId = null
            };
        }

        private static DateTime? ResolveProjectionSeenStateTimestamp(
            Notification? notification,
            IEnumerable<NotificationContribution>? activeContributions = null)
        {
            if (notification == null)
            {
                return null;
            }

            var resolvedActiveContributions = activeContributions?.ToList()
                ?? notification.Contributions
                    .Where(x => x.IsActive)
                    .OrderByDescending(x => x.UpdatedAt)
                    .ToList();
            if (resolvedActiveContributions.Count > 0)
            {
                return resolvedActiveContributions[0].UpdatedAt;
            }

            return notification.Contributions.Count == 0
                ? notification.LastEventAt
                : null;
        }

        private static bool ShouldAffectUnread(
            NotificationAggregateActionEnum action,
            DateTime? previousSeenStateTimestamp,
            DateTime? nextSeenStateTimestamp)
        {
            return action == NotificationAggregateActionEnum.Upsert &&
                   nextSeenStateTimestamp.HasValue &&
                   (!previousSeenStateTimestamp.HasValue ||
                    nextSeenStateTimestamp.Value > previousSeenStateTimestamp.Value);
        }

        private static NotificationProjectionResult Upsert(Guid recipientId, Guid? notificationId, bool affectsUnread = false)
        {
            return new NotificationProjectionResult
            {
                Action = NotificationProjectionActionEnum.Upsert,
                RecipientId = recipientId,
                NotificationId = notificationId,
                AffectsUnread = affectsUnread
            };
        }

        private static NotificationProjectionResult Remove(Guid recipientId, Guid notificationId)
        {
            return new NotificationProjectionResult
            {
                Action = NotificationProjectionActionEnum.Remove,
                RecipientId = recipientId,
                NotificationId = notificationId
            };
        }

        private static NotificationProjectionResult AttachDispatchMetadata(NotificationProjectionResult result, NotificationOutbox outbox)
        {
            result.EventId = outbox.OutboxId;
            result.OccurredAt = outbox.OccurredAt;
            return result;
        }
    }
}
