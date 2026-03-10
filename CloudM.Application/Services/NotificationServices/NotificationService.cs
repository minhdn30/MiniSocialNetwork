using CloudM.Application.DTOs.NotificationDTOs;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Repositories.Notifications;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CloudM.Application.Services.NotificationServices
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationOutboxRepository _notificationOutboxRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly AppDbContext _context;

        public NotificationService(
            INotificationOutboxRepository notificationOutboxRepository,
            INotificationRepository notificationRepository,
            AppDbContext context)
        {
            _notificationOutboxRepository = notificationOutboxRepository;
            _notificationRepository = notificationRepository;
            _context = context;
        }

        public async Task EnqueueAggregateEventAsync(NotificationAggregateEvent request, CancellationToken cancellationToken = default)
        {
            if (request.RecipientId == Guid.Empty || string.IsNullOrWhiteSpace(request.AggregateKey))
            {
                return;
            }

            var payload = new NotificationAggregateChangedPayload
            {
                Action = request.Action,
                Type = request.Type,
                AggregateKey = request.AggregateKey.Trim(),
                SourceType = request.SourceType,
                SourceId = request.SourceId,
                ActorId = request.ActorId,
                TargetKind = request.TargetKind,
                TargetId = request.TargetId,
                KeepWhenEmpty = request.KeepWhenEmpty,
                OccurredAt = request.OccurredAt == default ? DateTime.UtcNow : request.OccurredAt
            };

            var outbox = new NotificationOutbox
            {
                OutboxId = Guid.NewGuid(),
                EventType = NotificationOutboxEventTypes.AggregateChanged,
                RecipientId = request.RecipientId,
                PayloadJson = JsonSerializer.Serialize(payload),
                OccurredAt = payload.OccurredAt,
                Status = NotificationOutboxStatusEnum.Pending,
                AttemptCount = 0,
                LockedUntil = null,
                NextRetryAt = payload.OccurredAt,
                ProcessedAt = null,
                LastError = null
            };

            await _notificationOutboxRepository.AddAsync(outbox, cancellationToken);
        }

        public async Task EnqueueTargetUnavailableEventAsync(NotificationTargetUnavailableEvent request, CancellationToken cancellationToken = default)
        {
            if (request.RecipientId == Guid.Empty || request.TargetId == Guid.Empty)
            {
                return;
            }

            var payload = new NotificationTargetUnavailablePayload
            {
                Type = request.Type,
                AggregateKey = request.AggregateKey,
                TargetKind = request.TargetKind,
                TargetId = request.TargetId,
                OccurredAt = request.OccurredAt == default ? DateTime.UtcNow : request.OccurredAt
            };

            var outbox = new NotificationOutbox
            {
                OutboxId = Guid.NewGuid(),
                EventType = NotificationOutboxEventTypes.TargetUnavailable,
                RecipientId = request.RecipientId,
                PayloadJson = JsonSerializer.Serialize(payload),
                OccurredAt = payload.OccurredAt,
                Status = NotificationOutboxStatusEnum.Pending,
                AttemptCount = 0,
                LockedUntil = null,
                NextRetryAt = payload.OccurredAt,
                ProcessedAt = null,
                LastError = null
            };

            await _notificationOutboxRepository.AddAsync(outbox, cancellationToken);
        }

        public async Task EnqueueTargetUnavailableForExistingRecipientsAsync(
            NotificationTypeEnum type,
            NotificationTargetKindEnum targetKind,
            Guid targetId,
            Guid initiatorId,
            DateTime? occurredAt = null,
            CancellationToken cancellationToken = default)
        {
            if (targetId == Guid.Empty || initiatorId == Guid.Empty)
            {
                return;
            }

            var eventAt = occurredAt ?? DateTime.UtcNow;
            var payload = new NotificationTargetUnavailablePayload
            {
                Type = type,
                AggregateKey = null,
                TargetKind = targetKind,
                TargetId = targetId,
                OccurredAt = eventAt
            };
            var outbox = new NotificationOutbox
            {
                OutboxId = Guid.NewGuid(),
                EventType = NotificationOutboxEventTypes.TargetUnavailableBroadcast,
                RecipientId = initiatorId,
                PayloadJson = JsonSerializer.Serialize(payload),
                OccurredAt = eventAt,
                Status = NotificationOutboxStatusEnum.Pending,
                AttemptCount = 0,
                LockedUntil = null,
                NextRetryAt = eventAt,
                ProcessedAt = null,
                LastError = null
            };

            await _notificationOutboxRepository.AddAsync(outbox, cancellationToken);
        }

        public async Task<NotificationCursorResponse> GetNotificationsAsync(
            Guid recipientId,
            NotificationCursorRequest request,
            CancellationToken cancellationToken = default)
        {
            var safeRequest = request ?? new NotificationCursorRequest();
            var filter = (safeRequest.Filter ?? "all").Trim().ToLowerInvariant();
            var unreadOnly = filter == "unread";
            var limit = safeRequest.Limit <= 0 ? 20 : Math.Min(safeRequest.Limit, 50);
            var readState = await GetReadStateSnapshotAsync(recipientId, cancellationToken);

            var (items, nextCursorLastEventAt, nextCursorNotificationId) = await _notificationRepository.GetByCursorAsync(
                recipientId,
                readState.LastNotificationsSeenAt,
                unreadOnly,
                limit,
                safeRequest.CursorLastEventAt,
                safeRequest.CursorNotificationId,
                cancellationToken);

            var responseItems = await BuildNotificationItemsAsync(
                recipientId,
                items,
                readState.LastNotificationsSeenAt,
                cancellationToken);
            var followRequestCount = await GetPendingFollowRequestCountAsync(recipientId, cancellationToken);
            var unreadSummary = await BuildUnreadSummaryAsync(
                recipientId,
                readState,
                followRequestCount,
                cancellationToken);

            return new NotificationCursorResponse
            {
                AccountId = recipientId,
                Items = responseItems,
                Count = unreadSummary.Count,
                NotificationUnreadCount = unreadSummary.NotificationUnreadCount,
                FollowRequestUnreadCount = unreadSummary.FollowRequestUnreadCount,
                PendingFollowRequestCount = unreadSummary.PendingFollowRequestCount,
                FollowRequestCount = followRequestCount,
                LastNotificationsSeenAt = readState.LastNotificationsSeenAt,
                LastFollowRequestsSeenAt = readState.LastFollowRequestsSeenAt,
                NextCursor = nextCursorLastEventAt.HasValue && nextCursorNotificationId.HasValue
                    ? new NotificationNextCursorResponse
                    {
                        LastEventAt = nextCursorLastEventAt.Value,
                        NotificationId = nextCursorNotificationId.Value
                    }
                    : null
            };
        }

        public Task<NotificationUnreadSummaryResponse> GetUnreadSummaryAsync(
            Guid recipientId,
            CancellationToken cancellationToken = default)
        {
            return BuildUnreadSummaryAsync(recipientId, cancellationToken);
        }

        public Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken = default)
        {
            return GetUnreadCountCoreAsync(recipientId, cancellationToken);
        }

        public async Task<NotificationUnreadSummaryResponse> UpdateReadStateAsync(
            Guid recipientId,
            NotificationReadStateRequest request,
            CancellationToken cancellationToken = default)
        {
            var safeRequest = request ?? new NotificationReadStateRequest();
            var nowUtc = DateTime.UtcNow;
            var notificationsSeenAt = NormalizeSeenTimestamp(safeRequest.NotificationsSeenAt, nowUtc);
            var followRequestsSeenAt = NormalizeSeenTimestamp(safeRequest.FollowRequestsSeenAt, nowUtc);

            if (notificationsSeenAt.HasValue || followRequestsSeenAt.HasValue)
            {
                await UpsertReadStateAsync(
                    recipientId,
                    notificationsSeenAt,
                    followRequestsSeenAt,
                    cancellationToken);
            }

            return await BuildUnreadSummaryAsync(recipientId, cancellationToken);
        }

        private async Task<int> GetUnreadCountCoreAsync(Guid recipientId, CancellationToken cancellationToken)
        {
            var summary = await BuildUnreadSummaryAsync(recipientId, cancellationToken);
            return summary.Count;
        }

        private async Task<NotificationUnreadSummaryResponse> BuildUnreadSummaryAsync(
            Guid recipientId,
            CancellationToken cancellationToken)
        {
            return await BuildUnreadSummaryAsync(recipientId, null, null, cancellationToken);
        }

        private async Task<NotificationUnreadSummaryResponse> BuildUnreadSummaryAsync(
            Guid recipientId,
            NotificationReadStateSnapshot? readState,
            int? pendingFollowRequestCount,
            CancellationToken cancellationToken)
        {
            var effectiveReadState = readState ?? await GetReadStateSnapshotAsync(recipientId, cancellationToken);
            var notificationUnreadCount = await _notificationRepository.GetUnreadCountAsync(
                recipientId,
                effectiveReadState.LastNotificationsSeenAt,
                cancellationToken);
            var followRequestUnreadCount = await GetUnreadFollowRequestCountAsync(
                recipientId,
                effectiveReadState.LastFollowRequestsSeenAt,
                cancellationToken);
            var effectivePendingFollowRequestCount = pendingFollowRequestCount ??
                await GetPendingFollowRequestCountAsync(recipientId, cancellationToken);

            return new NotificationUnreadSummaryResponse
            {
                AccountId = recipientId,
                Count = notificationUnreadCount + followRequestUnreadCount,
                NotificationUnreadCount = notificationUnreadCount,
                FollowRequestUnreadCount = followRequestUnreadCount,
                PendingFollowRequestCount = effectivePendingFollowRequestCount,
                LastNotificationsSeenAt = effectiveReadState.LastNotificationsSeenAt,
                LastFollowRequestsSeenAt = effectiveReadState.LastFollowRequestsSeenAt
            };
        }

        private async Task<NotificationReadStateSnapshot> GetReadStateSnapshotAsync(
            Guid recipientId,
            CancellationToken cancellationToken)
        {
            var state = await _context.NotificationReadStates
                .AsNoTracking()
                .Where(x => x.AccountId == recipientId)
                .Select(x => new NotificationReadStateSnapshot
                {
                    LastNotificationsSeenAt = x.LastNotificationsSeenAt,
                    LastFollowRequestsSeenAt = x.LastFollowRequestsSeenAt
                })
                .SingleOrDefaultAsync(cancellationToken);

            return state ?? new NotificationReadStateSnapshot();
        }

        private async Task<int> GetUnreadFollowRequestCountAsync(
            Guid recipientId,
            DateTime? lastFollowRequestsSeenAt,
            CancellationToken cancellationToken)
        {
            var query = _context.FollowRequests
                .AsNoTracking()
                .Where(x =>
                    x.TargetId == recipientId &&
                    x.Requester.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(x.Requester.RoleId));

            if (lastFollowRequestsSeenAt.HasValue)
            {
                query = query.Where(x => x.CreatedAt > lastFollowRequestsSeenAt.Value);
            }

            return await query.CountAsync(cancellationToken);
        }

        private Task<int> GetPendingFollowRequestCountAsync(
            Guid recipientId,
            CancellationToken cancellationToken)
        {
            return _context.FollowRequests
                .AsNoTracking()
                .Where(x =>
                    x.TargetId == recipientId &&
                    x.Requester.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(x.Requester.RoleId))
                .CountAsync(cancellationToken);
        }

        private async Task UpsertReadStateAsync(
            Guid recipientId,
            DateTime? notificationsSeenAt,
            DateTime? followRequestsSeenAt,
            CancellationToken cancellationToken)
        {
            if (_context.Database.IsRelational())
            {
                var nowUtc = DateTime.UtcNow;
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ""NotificationReadStates"" (
    ""AccountId"",
    ""LastNotificationsSeenAt"",
    ""LastFollowRequestsSeenAt"",
    ""CreatedAt"",
    ""UpdatedAt"")
VALUES (
    {recipientId},
    {notificationsSeenAt},
    {followRequestsSeenAt},
    {nowUtc},
    {nowUtc})
ON CONFLICT (""AccountId"") DO UPDATE
SET
    ""LastNotificationsSeenAt"" = CASE
        WHEN EXCLUDED.""LastNotificationsSeenAt"" IS NULL THEN ""NotificationReadStates"".""LastNotificationsSeenAt""
        WHEN ""NotificationReadStates"".""LastNotificationsSeenAt"" IS NULL OR ""NotificationReadStates"".""LastNotificationsSeenAt"" < EXCLUDED.""LastNotificationsSeenAt"" THEN EXCLUDED.""LastNotificationsSeenAt""
        ELSE ""NotificationReadStates"".""LastNotificationsSeenAt""
    END,
    ""LastFollowRequestsSeenAt"" = CASE
        WHEN EXCLUDED.""LastFollowRequestsSeenAt"" IS NULL THEN ""NotificationReadStates"".""LastFollowRequestsSeenAt""
        WHEN ""NotificationReadStates"".""LastFollowRequestsSeenAt"" IS NULL OR ""NotificationReadStates"".""LastFollowRequestsSeenAt"" < EXCLUDED.""LastFollowRequestsSeenAt"" THEN EXCLUDED.""LastFollowRequestsSeenAt""
        ELSE ""NotificationReadStates"".""LastFollowRequestsSeenAt""
    END,
    ""UpdatedAt"" = CASE
        WHEN (
            EXCLUDED.""LastNotificationsSeenAt"" IS NOT NULL AND
            (""NotificationReadStates"".""LastNotificationsSeenAt"" IS NULL OR ""NotificationReadStates"".""LastNotificationsSeenAt"" < EXCLUDED.""LastNotificationsSeenAt"")
        ) OR (
            EXCLUDED.""LastFollowRequestsSeenAt"" IS NOT NULL AND
            (""NotificationReadStates"".""LastFollowRequestsSeenAt"" IS NULL OR ""NotificationReadStates"".""LastFollowRequestsSeenAt"" < EXCLUDED.""LastFollowRequestsSeenAt"")
        ) THEN {nowUtc}
        ELSE ""NotificationReadStates"".""UpdatedAt""
    END;", cancellationToken);
                return;
            }

            var state = await _context.NotificationReadStates
                .SingleOrDefaultAsync(x => x.AccountId == recipientId, cancellationToken);
            var now = DateTime.UtcNow;
            var hasChanges = false;

            if (state == null)
            {
                state = new NotificationReadState
                {
                    AccountId = recipientId,
                    LastNotificationsSeenAt = notificationsSeenAt,
                    LastFollowRequestsSeenAt = followRequestsSeenAt,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _context.NotificationReadStates.AddAsync(state, cancellationToken);
                hasChanges = notificationsSeenAt.HasValue || followRequestsSeenAt.HasValue;
            }
            else
            {
                if (notificationsSeenAt.HasValue &&
                    (!state.LastNotificationsSeenAt.HasValue || state.LastNotificationsSeenAt.Value < notificationsSeenAt.Value))
                {
                    state.LastNotificationsSeenAt = notificationsSeenAt.Value;
                    hasChanges = true;
                }

                if (followRequestsSeenAt.HasValue &&
                    (!state.LastFollowRequestsSeenAt.HasValue || state.LastFollowRequestsSeenAt.Value < followRequestsSeenAt.Value))
                {
                    state.LastFollowRequestsSeenAt = followRequestsSeenAt.Value;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    state.UpdatedAt = now;
                }
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task<List<NotificationItemResponse>> BuildNotificationItemsAsync(
            Guid recipientId,
            List<Notification> notifications,
            DateTime? lastNotificationsSeenAt,
            CancellationToken cancellationToken)
        {
            if (notifications.Count == 0)
            {
                return new List<NotificationItemResponse>();
            }

            var nowUtc = DateTime.UtcNow;

            var postTargetIds = notifications
                .Where(x => x.TargetKind == NotificationTargetKindEnum.Post && x.TargetId.HasValue)
                .Select(x => x.TargetId!.Value)
                .Distinct()
                .ToList();
            var storyTargetIds = notifications
                .Where(x => x.TargetKind == NotificationTargetKindEnum.Story && x.TargetId.HasValue)
                .Select(x => x.TargetId!.Value)
                .Distinct()
                .ToList();
            var accountTargetIds = notifications
                .Where(x => x.TargetKind == NotificationTargetKindEnum.Account && x.TargetId.HasValue)
                .Select(x => x.TargetId!.Value)
                .Distinct()
                .ToList();

            var postTargets = await _context.Posts
                .AsNoTracking()
                .Where(x => postTargetIds.Contains(x.PostId))
                .Select(x => new PostTargetProjection
                {
                    PostId = x.PostId,
                    PostCode = x.PostCode,
                    OwnerId = x.AccountId,
                    Privacy = x.Privacy,
                    IsDeleted = x.IsDeleted,
                    OwnerStatus = x.Account.Status,
                    OwnerRoleId = x.Account.RoleId,
                    ThumbnailUrl = x.Medias
                        .OrderBy(m => m.CreatedAt)
                        .Select(m => m.MediaUrl)
                        .FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.PostId, cancellationToken);

            var storyTargets = await _context.Stories
                .AsNoTracking()
                .Where(x => storyTargetIds.Contains(x.StoryId))
                .Select(x => new StoryTargetProjection
                {
                    StoryId = x.StoryId,
                    OwnerId = x.AccountId,
                    Privacy = x.Privacy,
                    IsDeleted = x.IsDeleted,
                    ExpiresAt = x.ExpiresAt,
                    OwnerStatus = x.Account.Status,
                    OwnerRoleId = x.Account.RoleId,
                    ThumbnailUrl = x.ContentType == StoryContentTypeEnum.Text ? null : x.MediaUrl
                })
                .ToDictionaryAsync(x => x.StoryId, cancellationToken);

            var accountTargets = await _context.Accounts
                .AsNoTracking()
                .Where(x => accountTargetIds.Contains(x.AccountId))
                .Select(x => new AccountTargetProjection
                {
                    AccountId = x.AccountId,
                    Status = x.Status,
                    RoleId = x.RoleId
                })
                .ToDictionaryAsync(x => x.AccountId, cancellationToken);

            var ownerIds = postTargets.Values
                .Select(x => x.OwnerId)
                .Concat(storyTargets.Values.Select(x => x.OwnerId))
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();

            var lastActorIds = notifications
                .Where(x => x.LastActorId.HasValue && x.LastActorId.Value != Guid.Empty)
                .Select(x => x.LastActorId!.Value)
                .Distinct()
                .ToList();
            var actorIdentityById = lastActorIds.Count == 0
                ? new Dictionary<Guid, ActorIdentityProjection>()
                : await _context.Accounts
                    .AsNoTracking()
                    .Where(x =>
                        lastActorIds.Contains(x.AccountId) &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(x.RoleId))
                    .Select(x => new ActorIdentityProjection
                    {
                        AccountId = x.AccountId,
                        Status = x.Status,
                        IsSocialEligible = true,
                        Username = x.Username,
                        FullName = x.FullName,
                        AvatarUrl = x.AvatarUrl
                    })
                    .ToDictionaryAsync(x => x.AccountId, cancellationToken);

            var actorSnapshotByNotificationId = notifications.ToDictionary(
                x => x.NotificationId,
                x => TryParseActorSnapshot(x.LastActorSnapshot));

            var notificationIdsNeedActorRefresh = notifications
                .Where(notification =>
                    notification.LastActorId == null ||
                    (notification.ActorCount > 0 &&
                     actorSnapshotByNotificationId[notification.NotificationId] == null) ||
                    (notification.LastActorId.HasValue &&
                     (!actorIdentityById.TryGetValue(notification.LastActorId.Value, out var actorIdentity) ||
                      actorIdentity.Status != AccountStatusEnum.Active ||
                      !actorIdentity.IsSocialEligible)))
                .Select(notification => notification.NotificationId)
                .Distinct()
                .ToList();

            var latestActorResolveByNotificationId = notificationIdsNeedActorRefresh.Count == 0
                ? new Dictionary<Guid, LatestActorResolveResult>()
                : await ResolveLatestActiveActorsAsync(notificationIdsNeedActorRefresh, cancellationToken);
            var seenStateMetadataByNotificationId = await ResolveSeenStateMetadataAsync(
                notifications.Select(x => x.NotificationId),
                cancellationToken);
            var commentNavigationByNotificationId = await ResolveCommentNavigationMetadataAsync(
                notifications,
                cancellationToken);

            var followedOwnerIds = ownerIds.Count == 0
                ? new HashSet<Guid>()
                : (await _context.Follows
                    .AsNoTracking()
                    .Where(x => x.FollowerId == recipientId && ownerIds.Contains(x.FollowedId))
                    .Select(x => x.FollowedId)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            var responseItems = new List<NotificationItemResponse>(notifications.Count);
            var fixupToUnavailableIds = new List<Guid>();
            var fixupToActiveIds = new List<Guid>();
            var shouldPersistActorFixup = false;

            foreach (var notification in notifications)
            {
                actorSnapshotByNotificationId.TryGetValue(notification.NotificationId, out var actorSnapshot);
                var shouldRefreshActor = latestActorResolveByNotificationId.ContainsKey(notification.NotificationId);
                if (shouldRefreshActor)
                {
                    var resolved = latestActorResolveByNotificationId[notification.NotificationId];
                    actorSnapshot = resolved.Actor;
                    if (resolved.ShouldPersistFixup)
                    {
                        shouldPersistActorFixup = true;
                        notification.ActorCount = resolved.ActorCount;
                        notification.EventCount = resolved.EventCount;
                        notification.LastActorId = resolved.LastActorId;
                        notification.LastActorSnapshot = resolved.LastActorSnapshotJson;
                    }

                    if (resolved.LastActorId.HasValue &&
                        resolved.Actor != null)
                    {
                        actorIdentityById[resolved.LastActorId.Value] = new ActorIdentityProjection
                        {
                            AccountId = resolved.LastActorId.Value,
                            Status = AccountStatusEnum.Active,
                            IsSocialEligible = true,
                            Username = resolved.Actor.Username,
                            FullName = resolved.Actor.FullName,
                            AvatarUrl = resolved.Actor.AvatarUrl
                        };
                    }
                }

                var isAvailableByTarget = IsAvailableByTarget(
                    recipientId,
                    notification,
                    postTargets,
                    storyTargets,
                    accountTargets,
                    followedOwnerIds,
                    nowUtc);

                var hasAvailableActor =
                    notification.ActorCount > 0 &&
                    notification.LastActorId.HasValue &&
                    actorSnapshot != null;
                var shouldBeUnavailable = !isAvailableByTarget || !hasAvailableActor;
                if (shouldBeUnavailable && notification.State != NotificationStateEnum.Unavailable)
                {
                    fixupToUnavailableIds.Add(notification.NotificationId);
                }
                else if (!shouldBeUnavailable && notification.State == NotificationStateEnum.Unavailable)
                {
                    fixupToActiveIds.Add(notification.NotificationId);
                }

                var actor = BuildNotificationActor(
                    notification.LastActorId,
                    actorSnapshot,
                    actorIdentityById);

                string? targetPostCode = null;
                string? thumbnailUrl = null;
                if (!shouldBeUnavailable && notification.TargetKind == NotificationTargetKindEnum.Post && notification.TargetId.HasValue)
                {
                    if (postTargets.TryGetValue(notification.TargetId.Value, out var postTarget))
                    {
                        targetPostCode = postTarget.PostCode;
                        thumbnailUrl = string.IsNullOrWhiteSpace(postTarget.ThumbnailUrl) ? null : postTarget.ThumbnailUrl;
                    }
                }

                if (!shouldBeUnavailable && notification.TargetKind == NotificationTargetKindEnum.Story && notification.TargetId.HasValue)
                {
                    if (storyTargets.TryGetValue(notification.TargetId.Value, out var storyTarget))
                    {
                        thumbnailUrl = string.IsNullOrWhiteSpace(storyTarget.ThumbnailUrl) ? null : storyTarget.ThumbnailUrl;
                    }
                }

                var actorDisplay = !string.IsNullOrWhiteSpace(actor?.Username)
                    ? actor!.Username
                    : "Someone";
                var text = shouldBeUnavailable
                    ? "This content is no longer available"
                    : BuildNotificationText(notification.Type, actorDisplay, notification.ActorCount);
                var targetId = ResolveResponseTargetId(notification, actor);
                var canOpen = CanOpenNotification(notification, shouldBeUnavailable, actor);
                seenStateMetadataByNotificationId.TryGetValue(notification.NotificationId, out var seenStateMetadata);
                var seenStateTimestamp = seenStateMetadata?.SeenStateTimestamp ?? notification.LastEventAt;
                var hasAnyContribution = seenStateMetadata?.HasAnyContribution == true;
                var tracksUnreadState = !hasAnyContribution || seenStateMetadata?.SeenStateTimestamp.HasValue == true;

                var isSeenByCurrentState = !tracksUnreadState ||
                    (lastNotificationsSeenAt.HasValue &&
                     seenStateTimestamp <= lastNotificationsSeenAt.Value);
                commentNavigationByNotificationId.TryGetValue(notification.NotificationId, out var commentNavigation);

                responseItems.Add(new NotificationItemResponse
                {
                    NotificationId = notification.NotificationId,
                    Type = (int)notification.Type,
                    State = shouldBeUnavailable ? (int)NotificationStateEnum.Unavailable : (int)NotificationStateEnum.Active,
                    CreatedAt = notification.CreatedAt,
                    LastEventAt = notification.LastEventAt,
                    SeenStateTimestamp = seenStateTimestamp,
                    IsSeenByCurrentState = isSeenByCurrentState,
                    TracksUnreadState = tracksUnreadState,
                    ActorCount = notification.ActorCount,
                    EventCount = notification.EventCount,
                    Actor = actor,
                    Text = text,
                    TargetKind = (int)notification.TargetKind,
                    TargetId = targetId,
                    TargetCommentId = commentNavigation?.TargetCommentId,
                    ParentCommentId = commentNavigation?.ParentCommentId,
                    TargetPostCode = targetPostCode,
                    ThumbnailUrl = thumbnailUrl,
                    CanOpen = canOpen
                });
            }

            if (fixupToUnavailableIds.Count > 0 || fixupToActiveIds.Count > 0)
            {
                var now = DateTime.UtcNow;
                if (_context.Database.IsRelational())
                {
                    if (fixupToUnavailableIds.Count > 0)
                    {
                        await _context.Notifications
                            .Where(x => fixupToUnavailableIds.Contains(x.NotificationId))
                            .ExecuteUpdateAsync(setters => setters
                                .SetProperty(x => x.State, NotificationStateEnum.Unavailable)
                                .SetProperty(x => x.UpdatedAt, now),
                                cancellationToken);
                    }

                    if (fixupToActiveIds.Count > 0)
                    {
                        await _context.Notifications
                            .Where(x => fixupToActiveIds.Contains(x.NotificationId))
                            .ExecuteUpdateAsync(setters => setters
                                .SetProperty(x => x.State, NotificationStateEnum.Active)
                                .SetProperty(x => x.UpdatedAt, now),
                                cancellationToken);
                    }
                }
                else
                {
                    var fixupIds = fixupToUnavailableIds
                        .Concat(fixupToActiveIds)
                        .Distinct()
                        .ToList();
                    var fixupNotifications = await _context.Notifications
                        .Where(x => fixupIds.Contains(x.NotificationId))
                        .ToListAsync(cancellationToken);
                    foreach (var fixupNotification in fixupNotifications)
                    {
                        fixupNotification.State = fixupToUnavailableIds.Contains(fixupNotification.NotificationId)
                            ? NotificationStateEnum.Unavailable
                            : NotificationStateEnum.Active;
                        fixupNotification.UpdatedAt = now;
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            if (shouldPersistActorFixup)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return responseItems
                .OrderByDescending(x => x.LastEventAt)
                .ThenByDescending(x => x.NotificationId)
                .ToList();
        }

        private async Task<Dictionary<Guid, NotificationSeenStateMetadata>> ResolveSeenStateMetadataAsync(
            IEnumerable<Guid> notificationIds,
            CancellationToken cancellationToken)
        {
            var safeNotificationIds = (notificationIds ?? Enumerable.Empty<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();
            if (safeNotificationIds.Count == 0)
            {
                return new Dictionary<Guid, NotificationSeenStateMetadata>();
            }

            return await _context.NotificationContributions
                .AsNoTracking()
                .Where(x =>
                    safeNotificationIds.Contains(x.NotificationId) &&
                    x.Actor.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(x.Actor.RoleId))
                .GroupBy(x => x.NotificationId)
                .Select(x => new
                {
                    NotificationId = x.Key,
                    SeenStateTimestamp = x
                        .Where(item => item.IsActive)
                        .Select(item => (DateTime?)item.UpdatedAt)
                        .Max()
                })
                .ToDictionaryAsync(
                    x => x.NotificationId,
                    x => new NotificationSeenStateMetadata
                    {
                        NotificationId = x.NotificationId,
                        HasAnyContribution = true,
                        SeenStateTimestamp = x.SeenStateTimestamp
                    },
                    cancellationToken);
        }

        private async Task<Dictionary<Guid, NotificationCommentNavigationMetadata>> ResolveCommentNavigationMetadataAsync(
            IEnumerable<Notification> notifications,
            CancellationToken cancellationToken)
        {
            var safeNotifications = (notifications ?? Enumerable.Empty<Notification>())
                .Where(x => x != null && x.NotificationId != Guid.Empty)
                .ToList();
            if (safeNotifications.Count == 0)
            {
                return new Dictionary<Guid, NotificationCommentNavigationMetadata>();
            }

            var result = new Dictionary<Guid, NotificationCommentNavigationMetadata>();
            var latestContributionNotificationIds = safeNotifications
                .Where(x =>
                    x.Type == NotificationTypeEnum.PostComment ||
                    x.Type == NotificationTypeEnum.CommentReply)
                .Select(x => x.NotificationId)
                .Distinct()
                .ToList();

            var latestContributionByNotificationId = latestContributionNotificationIds.Count == 0
                ? new Dictionary<Guid, LatestContributionNavigationProjection>()
                : (await _context.NotificationContributions
                    .AsNoTracking()
                    .Where(x =>
                        latestContributionNotificationIds.Contains(x.NotificationId) &&
                        x.IsActive &&
                        x.Actor.Status == AccountStatusEnum.Active &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(x.Actor.RoleId))
                    .Select(x => new LatestContributionNavigationProjection
                    {
                        NotificationId = x.NotificationId,
                        SourceId = x.SourceId,
                        UpdatedAt = x.UpdatedAt
                    })
                    .ToListAsync(cancellationToken))
                    .GroupBy(x => x.NotificationId)
                    .ToDictionary(
                        x => x.Key,
                        x => x
                            .OrderByDescending(item => item.UpdatedAt)
                            .ThenByDescending(item => item.SourceId)
                            .First());

            var targetCommentIds = new HashSet<Guid>();

            foreach (var notification in safeNotifications)
            {
                var metadata = ResolveCommentNavigationMetadata(
                    notification,
                    latestContributionByNotificationId);
                if (metadata == null)
                {
                    continue;
                }

                result[notification.NotificationId] = metadata;
                if (metadata.TargetCommentId.HasValue && metadata.TargetCommentId.Value != Guid.Empty)
                {
                    targetCommentIds.Add(metadata.TargetCommentId.Value);
                }
            }

            if (targetCommentIds.Count == 0)
            {
                return result;
            }

            var commentParentLookup = await _context.Comments
                .AsNoTracking()
                .Where(x => targetCommentIds.Contains(x.CommentId))
                .Select(x => new CommentNavigationLookupProjection
                {
                    CommentId = x.CommentId,
                    ParentCommentId = x.ParentCommentId
                })
                .ToDictionaryAsync(x => x.CommentId, cancellationToken);

            foreach (var metadata in result.Values)
            {
                if (!metadata.TargetCommentId.HasValue || metadata.TargetCommentId.Value == Guid.Empty)
                {
                    continue;
                }

                if (commentParentLookup.TryGetValue(metadata.TargetCommentId.Value, out var lookup))
                {
                    metadata.ParentCommentId ??= lookup.ParentCommentId;
                }
            }

            return result;
        }

        private static NotificationCommentNavigationMetadata? ResolveCommentNavigationMetadata(
            Notification notification,
            IReadOnlyDictionary<Guid, LatestContributionNavigationProjection> latestContributionByNotificationId)
        {
            if (notification == null)
            {
                return null;
            }

            if (notification.Type == NotificationTypeEnum.PostComment)
            {
                if (!latestContributionByNotificationId.TryGetValue(notification.NotificationId, out var latestContribution) ||
                    latestContribution.SourceId == Guid.Empty)
                {
                    return null;
                }

                return new NotificationCommentNavigationMetadata
                {
                    TargetCommentId = latestContribution.SourceId
                };
            }

            if (notification.Type == NotificationTypeEnum.CommentReply)
            {
                if (!latestContributionByNotificationId.TryGetValue(notification.NotificationId, out var latestContribution) ||
                    latestContribution.SourceId == Guid.Empty)
                {
                    return null;
                }

                return new NotificationCommentNavigationMetadata
                {
                    TargetCommentId = latestContribution.SourceId,
                    ParentCommentId = TryParseNotificationAggregateGuid(notification.AggregateKey)
                };
            }

            if (notification.Type == NotificationTypeEnum.CommentMention ||
                notification.Type == NotificationTypeEnum.CommentReact ||
                notification.Type == NotificationTypeEnum.ReplyReact)
            {
                var targetCommentId = TryParseNotificationAggregateGuid(notification.AggregateKey);
                if (!targetCommentId.HasValue || targetCommentId.Value == Guid.Empty)
                {
                    return null;
                }

                return new NotificationCommentNavigationMetadata
                {
                    TargetCommentId = targetCommentId.Value
                };
            }

            return null;
        }

        private static Guid? TryParseNotificationAggregateGuid(string? aggregateKey)
        {
            var raw = (aggregateKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var separatorIndex = raw.IndexOf(':');
            if (separatorIndex < 0 || separatorIndex >= raw.Length - 1)
            {
                return null;
            }

            return Guid.TryParse(raw[(separatorIndex + 1)..], out var parsed)
                ? parsed
                : null;
        }

        private async Task<Dictionary<Guid, LatestActorResolveResult>> ResolveLatestActiveActorsAsync(
            IEnumerable<Guid> notificationIds,
            CancellationToken cancellationToken)
        {
            var safeNotificationIds = (notificationIds ?? Enumerable.Empty<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();
            if (safeNotificationIds.Count == 0)
            {
                return new Dictionary<Guid, LatestActorResolveResult>();
            }

            var activeContributions = await _context.NotificationContributions
                .AsNoTracking()
                .Where(x =>
                    safeNotificationIds.Contains(x.NotificationId) &&
                    x.IsActive &&
                    x.Actor.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(x.Actor.RoleId))
                .Select(x => new
                {
                    x.NotificationId,
                    x.ActorId,
                    x.UpdatedAt,
                    x.Actor.Username,
                    x.Actor.FullName,
                    x.Actor.AvatarUrl
                })
                .ToListAsync(cancellationToken);

            var contributionsByNotificationId = activeContributions
                .GroupBy(x => x.NotificationId)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderByDescending(item => item.UpdatedAt).ToList());

            var result = new Dictionary<Guid, LatestActorResolveResult>(safeNotificationIds.Count);
            foreach (var notificationId in safeNotificationIds)
            {
                if (!contributionsByNotificationId.TryGetValue(notificationId, out var contributionsForNotification) ||
                    contributionsForNotification.Count == 0)
                {
                    result[notificationId] = new LatestActorResolveResult
                    {
                        Actor = null,
                        ActorCount = 0,
                        EventCount = 0,
                        LastActorId = null,
                        LastActorSnapshotJson = null,
                        ShouldPersistFixup = true
                    };
                    continue;
                }

                var latest = contributionsForNotification[0];
                var actor = new NotificationActorSnapshot
                {
                    AccountId = latest.ActorId,
                    Username = latest.Username,
                    FullName = latest.FullName,
                    AvatarUrl = latest.AvatarUrl
                };

                result[notificationId] = new LatestActorResolveResult
                {
                    Actor = actor,
                    ActorCount = contributionsForNotification.Select(x => x.ActorId).Distinct().Count(),
                    EventCount = contributionsForNotification.Count,
                    LastActorId = latest.ActorId,
                    LastActorSnapshotJson = JsonSerializer.Serialize(actor),
                    ShouldPersistFixup = true
                };
            }

            return result;
        }

        private static NotificationActorSnapshot? TryParseActorSnapshot(string? rawSnapshot)
        {
            if (string.IsNullOrWhiteSpace(rawSnapshot))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<NotificationActorSnapshot>(rawSnapshot);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsAvailableByTarget(
            Guid recipientId,
            Notification notification,
            IReadOnlyDictionary<Guid, PostTargetProjection> postTargets,
            IReadOnlyDictionary<Guid, StoryTargetProjection> storyTargets,
            IReadOnlyDictionary<Guid, AccountTargetProjection> accountTargets,
            IReadOnlySet<Guid> followedOwnerIds,
            DateTime nowUtc)
        {
            if (!notification.TargetId.HasValue || notification.TargetId.Value == Guid.Empty || notification.TargetKind == NotificationTargetKindEnum.None)
            {
                return true;
            }

            if (notification.TargetKind == NotificationTargetKindEnum.Account)
            {
                return accountTargets.TryGetValue(notification.TargetId.Value, out var accountTarget)
                    && accountTarget.Status == AccountStatusEnum.Active
                    && SocialRoleRules.IsSocialEligibleRole(accountTarget.RoleId);
            }

            if (notification.TargetKind == NotificationTargetKindEnum.Post)
            {
                if (!postTargets.TryGetValue(notification.TargetId.Value, out var postTarget))
                {
                    return false;
                }

                if (postTarget.IsDeleted ||
                    postTarget.OwnerStatus != AccountStatusEnum.Active ||
                    !SocialRoleRules.IsSocialEligibleRole(postTarget.OwnerRoleId))
                {
                    return false;
                }

                if (postTarget.OwnerId == recipientId)
                {
                    return true;
                }

                if (postTarget.Privacy == PostPrivacyEnum.Public)
                {
                    return true;
                }

                if (postTarget.Privacy == PostPrivacyEnum.FollowOnly)
                {
                    return followedOwnerIds.Contains(postTarget.OwnerId);
                }

                return false;
            }

            if (notification.TargetKind == NotificationTargetKindEnum.Story)
            {
                if (!storyTargets.TryGetValue(notification.TargetId.Value, out var storyTarget))
                {
                    return false;
                }

                if (storyTarget.IsDeleted ||
                    storyTarget.OwnerStatus != AccountStatusEnum.Active ||
                    !SocialRoleRules.IsSocialEligibleRole(storyTarget.OwnerRoleId))
                {
                    return false;
                }

                if (storyTarget.OwnerId == recipientId)
                {
                    return true;
                }

                if (storyTarget.ExpiresAt <= nowUtc)
                {
                    return false;
                }

                if (storyTarget.Privacy == StoryPrivacyEnum.Public)
                {
                    return true;
                }

                if (storyTarget.Privacy != StoryPrivacyEnum.FollowOnly)
                {
                    return false;
                }

                return followedOwnerIds.Contains(storyTarget.OwnerId);
            }

            return false;
        }

        private static string BuildNotificationText(NotificationTypeEnum type, string actorDisplayName, int actorCount)
        {
            var safeActor = string.IsNullOrWhiteSpace(actorDisplayName) ? "Someone" : actorDisplayName.Trim();
            var otherCount = Math.Max(actorCount - 1, 0);
            var actorLabel = otherCount > 0 ? $"{safeActor} and {otherCount} others" : safeActor;

            return type switch
            {
                NotificationTypeEnum.Follow => $"{actorLabel} followed you",
                NotificationTypeEnum.FollowRequest => $"{actorLabel} wants to follow you",
                NotificationTypeEnum.FollowRequestAccepted => $"{actorLabel} accepted your follow request",
                NotificationTypeEnum.PostComment => $"{actorLabel} commented on your post",
                NotificationTypeEnum.CommentReply => $"{actorLabel} replied to your comment",
                NotificationTypeEnum.PostTag => $"{actorLabel} tagged you in a post",
                NotificationTypeEnum.CommentMention => $"{actorLabel} mentioned you in a comment",
                NotificationTypeEnum.StoryReply => $"{actorLabel} replied to your story",
                NotificationTypeEnum.PostReact => $"{actorLabel} reacted to your post",
                NotificationTypeEnum.StoryReact => $"{actorLabel} reacted to your story",
                NotificationTypeEnum.CommentReact => $"{actorLabel} reacted to your comment",
                NotificationTypeEnum.ReplyReact => $"{actorLabel} reacted to your reply",
                _ => $"{actorLabel} sent a notification"
            };
        }

        private static bool CanOpenNotification(
            Notification notification,
            bool shouldBeUnavailable,
            NotificationActorResponse? actor)
        {
            if (shouldBeUnavailable)
            {
                return false;
            }

            if (notification.Type == NotificationTypeEnum.Follow &&
                notification.TargetKind == NotificationTargetKindEnum.Account)
            {
                return notification.ActorCount == 1 &&
                    actor != null &&
                    actor.AccountId != Guid.Empty;
            }

            return true;
        }

        private static Guid? ResolveResponseTargetId(
            Notification notification,
            NotificationActorResponse? actor)
        {
            if (notification.Type == NotificationTypeEnum.Follow &&
                notification.TargetKind == NotificationTargetKindEnum.Account &&
                notification.ActorCount == 1 &&
                actor != null &&
                actor.AccountId != Guid.Empty)
            {
                return actor.AccountId;
            }

            return notification.TargetId;
        }

        private static NotificationActorResponse? BuildNotificationActor(
            Guid? lastActorId,
            NotificationActorSnapshot? actorSnapshot,
            IReadOnlyDictionary<Guid, ActorIdentityProjection> actorIdentityById)
        {
            if (lastActorId.HasValue &&
                actorIdentityById.TryGetValue(lastActorId.Value, out var actorIdentity) &&
                actorIdentity.Status == AccountStatusEnum.Active &&
                actorIdentity.IsSocialEligible)
            {
                return new NotificationActorResponse
                {
                    AccountId = actorIdentity.AccountId,
                    Username = actorIdentity.Username,
                    FullName = actorIdentity.FullName,
                    AvatarUrl = actorIdentity.AvatarUrl
                };
            }

            if (actorSnapshot == null)
            {
                return null;
            }

            return new NotificationActorResponse
            {
                AccountId = actorSnapshot.AccountId,
                Username = actorSnapshot.Username,
                FullName = actorSnapshot.FullName,
                AvatarUrl = actorSnapshot.AvatarUrl
            };
        }

        private static DateTime? NormalizeUtc(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
        }

        private static DateTime? NormalizeSeenTimestamp(DateTime? value, DateTime nowUtc)
        {
            var normalized = NormalizeUtc(value);
            if (!normalized.HasValue)
            {
                return null;
            }

            return normalized.Value > nowUtc.AddMinutes(1)
                ? null
                : normalized.Value;
        }

        private sealed class PostTargetProjection
        {
            public Guid PostId { get; set; }
            public string PostCode { get; set; } = string.Empty;
            public Guid OwnerId { get; set; }
            public PostPrivacyEnum Privacy { get; set; }
            public bool IsDeleted { get; set; }
            public AccountStatusEnum OwnerStatus { get; set; }
            public int OwnerRoleId { get; set; }
            public string? ThumbnailUrl { get; set; }
        }

        private sealed class StoryTargetProjection
        {
            public Guid StoryId { get; set; }
            public Guid OwnerId { get; set; }
            public StoryPrivacyEnum Privacy { get; set; }
            public bool IsDeleted { get; set; }
            public DateTime ExpiresAt { get; set; }
            public AccountStatusEnum OwnerStatus { get; set; }
            public int OwnerRoleId { get; set; }
            public string? ThumbnailUrl { get; set; }
        }

        private sealed class AccountTargetProjection
        {
            public Guid AccountId { get; set; }
            public AccountStatusEnum Status { get; set; }
            public int RoleId { get; set; }
        }

        private sealed class ActorIdentityProjection
        {
            public Guid AccountId { get; set; }
            public AccountStatusEnum Status { get; set; }
            public bool IsSocialEligible { get; set; }
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string? AvatarUrl { get; set; }
        }

        private sealed class LatestActorResolveResult
        {
            public NotificationActorSnapshot? Actor { get; set; }
            public int ActorCount { get; set; }
            public int EventCount { get; set; }
            public Guid? LastActorId { get; set; }
            public string? LastActorSnapshotJson { get; set; }
            public bool ShouldPersistFixup { get; set; }
        }

        private sealed class NotificationSeenStateMetadata
        {
            public Guid NotificationId { get; set; }
            public bool HasAnyContribution { get; set; }
            public DateTime? SeenStateTimestamp { get; set; }
        }

        private sealed class LatestContributionNavigationProjection
        {
            public Guid NotificationId { get; set; }
            public Guid SourceId { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        private sealed class CommentNavigationLookupProjection
        {
            public Guid CommentId { get; set; }
            public Guid? ParentCommentId { get; set; }
        }

        private sealed class NotificationCommentNavigationMetadata
        {
            public Guid? TargetCommentId { get; set; }
            public Guid? ParentCommentId { get; set; }
        }

        private sealed class NotificationReadStateSnapshot
        {
            public DateTime? LastNotificationsSeenAt { get; set; }
            public DateTime? LastFollowRequestsSeenAt { get; set; }
        }
    }
}
