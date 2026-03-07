using CloudM.Application.Services.NotificationServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.FollowRequests;
using CloudM.Infrastructure.Repositories.UnitOfWork;

namespace CloudM.Application.Services.FollowServices
{
    public class FollowAutoAcceptService : IFollowAutoAcceptService
    {
        private readonly IFollowRequestRepository _followRequestRepository;
        private readonly IFollowRepository _followRepository;
        private readonly INotificationService _notificationService;
        private readonly IRealtimeService _realtimeService;
        private readonly IUnitOfWork _unitOfWork;

        public FollowAutoAcceptService(
            IFollowRequestRepository followRequestRepository,
            IFollowRepository followRepository,
            INotificationService notificationService,
            IRealtimeService realtimeService,
            IUnitOfWork unitOfWork)
        {
            _followRequestRepository = followRequestRepository;
            _followRepository = followRepository;
            _notificationService = notificationService;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> ProcessPendingBatchAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            var batchState = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var claimedItems = await _followRequestRepository.ClaimAutoAcceptBatchAsync(batchSize, cancellationToken);
                if (claimedItems.Count == 0)
                {
                    return FollowAutoAcceptBatchState.Empty;
                }

                var eventAt = DateTime.UtcNow;
                foreach (var item in claimedItems)
                {
                    await _notificationService.EnqueueAggregateEventAsync(
                        new NotificationAggregateEvent
                        {
                            RecipientId = item.TargetId,
                            Action = NotificationAggregateActionEnum.Deactivate,
                            Type = NotificationTypeEnum.FollowRequest,
                            AggregateKey = NotificationAggregateKeys.FollowRequest(item.RequesterId),
                            SourceType = NotificationSourceTypeEnum.FollowRequest,
                            SourceId = item.RequesterId,
                            ActorId = item.RequesterId,
                            TargetKind = NotificationTargetKindEnum.Account,
                            TargetId = item.RequesterId,
                            KeepWhenEmpty = false,
                            OccurredAt = eventAt
                        },
                        cancellationToken);
                }

                var activeItems = claimedItems
                    .Where(x => x.RequesterStatus == (int)AccountStatusEnum.Active)
                    .ToList();
                var insertedActiveItems = new List<ClaimedAutoAcceptFollowRequest>();

                if (activeItems.Count > 0)
                {
                    var insertedFollows = await _followRepository.AddFollowsIgnoreExistingAsync(
                        activeItems.Select(x => new Follow
                        {
                            FollowerId = x.RequesterId,
                            FollowedId = x.TargetId,
                            CreatedAt = eventAt
                        }),
                        cancellationToken);
                    var insertedFollowKeys = insertedFollows
                        .Select(x => BuildFollowKey(x.FollowerId, x.FollowedId))
                        .ToHashSet(StringComparer.Ordinal);
                    insertedActiveItems = activeItems
                        .Where(x => insertedFollowKeys.Contains(BuildFollowKey(x.RequesterId, x.TargetId)))
                        .ToList();

                    foreach (var item in insertedActiveItems)
                    {
                        await _notificationService.EnqueueAggregateEventAsync(
                            new NotificationAggregateEvent
                            {
                                RecipientId = item.TargetId,
                                Action = NotificationAggregateActionEnum.Upsert,
                                Type = NotificationTypeEnum.Follow,
                                AggregateKey = NotificationAggregateKeys.FollowAutoAcceptSummary(item.TargetId),
                                SourceType = NotificationSourceTypeEnum.FollowRelation,
                                SourceId = item.RequesterId,
                                ActorId = item.RequesterId,
                                TargetKind = NotificationTargetKindEnum.Account,
                                TargetId = item.RequesterId,
                                KeepWhenEmpty = false,
                                OccurredAt = eventAt
                            },
                            cancellationToken);

                        await _notificationService.EnqueueAggregateEventAsync(
                            new NotificationAggregateEvent
                            {
                                RecipientId = item.RequesterId,
                                Action = NotificationAggregateActionEnum.Upsert,
                                Type = NotificationTypeEnum.FollowRequestAccepted,
                                AggregateKey = NotificationAggregateKeys.FollowRequestAccepted(item.TargetId),
                                SourceType = NotificationSourceTypeEnum.FollowRequestAccepted,
                                SourceId = item.TargetId,
                                ActorId = item.TargetId,
                                TargetKind = NotificationTargetKindEnum.Account,
                                TargetId = item.TargetId,
                                KeepWhenEmpty = false,
                                OccurredAt = eventAt
                            },
                            cancellationToken);
                    }
                }

                var countsByAccountId = insertedActiveItems.Count > 0
                    ? await _followRepository.GetFollowCountsByAccountIdsAsync(
                        insertedActiveItems
                            .Select(x => x.TargetId)
                            .Concat(insertedActiveItems.Select(x => x.RequesterId))
                            .Distinct(),
                        cancellationToken)
                    : new Dictionary<Guid, (int Followers, int Following)>();

                return FollowAutoAcceptBatchState.Create(claimedItems, insertedActiveItems, countsByAccountId);
            });

            if (batchState.ClaimedCount == 0)
            {
                return 0;
            }

            foreach (var targetSync in batchState.TargetSyncs)
            {
                await _realtimeService.NotifyFollowStatsChangedAsync(
                    targetSync.TargetId,
                    targetSync.Followers,
                    targetSync.Following);
            }

            foreach (var requesterSync in batchState.RequesterSyncs)
            {
                await _realtimeService.NotifyCurrentUserFollowChangedAsync(
                    requesterSync.RequesterId,
                    requesterSync.TargetId,
                    "follow_request_accepted",
                    requesterSync.Followers,
                    requesterSync.Following);
            }

            foreach (var targetId in batchState.QueueTargetIds)
            {
                await _realtimeService.NotifyFollowRequestQueueChangedAsync(targetId, "refresh");
            }

            return batchState.ClaimedCount;
        }

        private sealed class FollowAutoAcceptBatchState
        {
            public static readonly FollowAutoAcceptBatchState Empty = new();

            public int ClaimedCount { get; init; }
            public List<TargetFollowSyncItem> TargetSyncs { get; init; } = new();
            public List<RequesterFollowSyncItem> RequesterSyncs { get; init; } = new();
            public List<Guid> QueueTargetIds { get; init; } = new();

            public static FollowAutoAcceptBatchState Create(
                List<ClaimedAutoAcceptFollowRequest> claimedItems,
                List<ClaimedAutoAcceptFollowRequest> activeItems,
                Dictionary<Guid, (int Followers, int Following)> countsByAccountId)
            {
                var targetSyncs = activeItems
                    .Select(x => x.TargetId)
                    .Distinct()
                    .Select(targetId =>
                    {
                        countsByAccountId.TryGetValue(targetId, out var counts);
                        return new TargetFollowSyncItem
                        {
                            TargetId = targetId,
                            Followers = counts.Followers,
                            Following = counts.Following
                        };
                    })
                    .ToList();

                var requesterSyncs = activeItems
                    .Select(item =>
                    {
                        countsByAccountId.TryGetValue(item.RequesterId, out var counts);
                        return new RequesterFollowSyncItem
                        {
                            RequesterId = item.RequesterId,
                            TargetId = item.TargetId,
                            Followers = counts.Followers,
                            Following = counts.Following
                        };
                    })
                    .ToList();

                return new FollowAutoAcceptBatchState
                {
                    ClaimedCount = claimedItems.Count,
                    TargetSyncs = targetSyncs,
                    RequesterSyncs = requesterSyncs,
                    QueueTargetIds = claimedItems
                        .Select(x => x.TargetId)
                        .Distinct()
                        .ToList()
                };
            }
        }

        private sealed class TargetFollowSyncItem
        {
            public Guid TargetId { get; init; }
            public int Followers { get; init; }
            public int Following { get; init; }
        }

        private sealed class RequesterFollowSyncItem
        {
            public Guid RequesterId { get; init; }
            public Guid TargetId { get; init; }
            public int Followers { get; init; }
            public int Following { get; init; }
        }

        private static string BuildFollowKey(Guid followerId, Guid followedId)
        {
            return $"{followerId:D}:{followedId:D}";
        }
    }
}
