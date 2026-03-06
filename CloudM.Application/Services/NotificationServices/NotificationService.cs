using CloudM.Application.DTOs.NotificationDTOs;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
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

            var (items, nextCursorLastEventAt, nextCursorNotificationId) = await _notificationRepository.GetByCursorAsync(
                recipientId,
                unreadOnly,
                limit,
                safeRequest.CursorLastEventAt,
                safeRequest.CursorNotificationId,
                cancellationToken);

            var responseItems = await BuildNotificationItemsAsync(recipientId, items, cancellationToken);

            return new NotificationCursorResponse
            {
                Items = responseItems,
                NextCursor = nextCursorLastEventAt.HasValue && nextCursorNotificationId.HasValue
                    ? new NotificationNextCursorResponse
                    {
                        LastEventAt = nextCursorLastEventAt.Value,
                        NotificationId = nextCursorNotificationId.Value
                    }
                    : null
            };
        }

        public Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken = default)
        {
            return _notificationRepository.GetUnreadCountAsync(recipientId, cancellationToken);
        }

        private async Task<List<NotificationItemResponse>> BuildNotificationItemsAsync(
            Guid recipientId,
            List<Notification> notifications,
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
                    ThumbnailUrl = x.ContentType == StoryContentTypeEnum.Text ? null : x.MediaUrl
                })
                .ToDictionaryAsync(x => x.StoryId, cancellationToken);

            var accountTargets = await _context.Accounts
                .AsNoTracking()
                .Where(x => accountTargetIds.Contains(x.AccountId))
                .Select(x => new AccountTargetProjection
                {
                    AccountId = x.AccountId,
                    Status = x.Status
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
            var actorStatusMap = lastActorIds.Count == 0
                ? new Dictionary<Guid, AccountStatusEnum>()
                : await _context.Accounts
                    .AsNoTracking()
                    .Where(x => lastActorIds.Contains(x.AccountId))
                    .Select(x => new { x.AccountId, x.Status })
                    .ToDictionaryAsync(x => x.AccountId, x => x.Status, cancellationToken);

            var actorSnapshotByNotificationId = notifications.ToDictionary(
                x => x.NotificationId,
                x => TryParseActorSnapshot(x.LastActorSnapshot));

            var notificationIdsNeedActorRefresh = notifications
                .Where(notification =>
                    notification.LastActorId == null ||
                    (notification.ActorCount > 0 &&
                     actorSnapshotByNotificationId[notification.NotificationId] == null) ||
                    (notification.LastActorId.HasValue &&
                     (!actorStatusMap.TryGetValue(notification.LastActorId.Value, out var actorStatus) ||
                      actorStatus != AccountStatusEnum.Active)))
                .Select(notification => notification.NotificationId)
                .Distinct()
                .ToList();

            var latestActorResolveByNotificationId = notificationIdsNeedActorRefresh.Count == 0
                ? new Dictionary<Guid, LatestActorResolveResult>()
                : await ResolveLatestActiveActorsAsync(notificationIdsNeedActorRefresh, cancellationToken);

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

                var actor = actorSnapshot == null
                    ? null
                    : new NotificationActorResponse
                    {
                        AccountId = actorSnapshot.AccountId,
                        Username = actorSnapshot.Username,
                        FullName = actorSnapshot.FullName,
                        AvatarUrl = actorSnapshot.AvatarUrl
                    };

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

                responseItems.Add(new NotificationItemResponse
                {
                    NotificationId = notification.NotificationId,
                    Type = (int)notification.Type,
                    State = shouldBeUnavailable ? (int)NotificationStateEnum.Unavailable : (int)NotificationStateEnum.Active,
                    IsRead = notification.IsRead,
                    CreatedAt = notification.CreatedAt,
                    LastEventAt = notification.LastEventAt,
                    ActorCount = notification.ActorCount,
                    EventCount = notification.EventCount,
                    Actor = actor,
                    Text = text,
                    TargetKind = (int)notification.TargetKind,
                    TargetId = notification.TargetId,
                    TargetPostCode = targetPostCode,
                    ThumbnailUrl = thumbnailUrl,
                    CanOpen = !shouldBeUnavailable
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
                    x.Actor.Status == AccountStatusEnum.Active)
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
                    && accountTarget.Status == AccountStatusEnum.Active;
            }

            if (notification.TargetKind == NotificationTargetKindEnum.Post)
            {
                if (!postTargets.TryGetValue(notification.TargetId.Value, out var postTarget))
                {
                    return false;
                }

                if (postTarget.IsDeleted || postTarget.OwnerStatus != AccountStatusEnum.Active)
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
                    storyTarget.ExpiresAt <= nowUtc ||
                    storyTarget.OwnerStatus != AccountStatusEnum.Active)
                {
                    return false;
                }

                if (storyTarget.OwnerId == recipientId)
                {
                    return true;
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
                _ => $"{actorLabel} sent a notification"
            };
        }

        private sealed class PostTargetProjection
        {
            public Guid PostId { get; set; }
            public string PostCode { get; set; } = string.Empty;
            public Guid OwnerId { get; set; }
            public PostPrivacyEnum Privacy { get; set; }
            public bool IsDeleted { get; set; }
            public AccountStatusEnum OwnerStatus { get; set; }
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
            public string? ThumbnailUrl { get; set; }
        }

        private sealed class AccountTargetProjection
        {
            public Guid AccountId { get; set; }
            public AccountStatusEnum Status { get; set; }
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
    }
}
