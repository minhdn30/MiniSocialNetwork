using CloudM.Application.DTOs.BlockDTOs;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.Services.NotificationServices;
using CloudM.Application.Services.PresenceServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.FollowRequests;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.BlockServices
{
    public class BlockService : IBlockService
    {
        private readonly IAccountBlockRepository _accountBlockRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IFollowRepository _followRepository;
        private readonly IFollowRequestRepository _followRequestRepository;
        private readonly INotificationService _notificationService;
        private readonly IOnlinePresenceService _onlinePresenceService;
        private readonly IRealtimeService _realtimeService;
        private readonly IUnitOfWork _unitOfWork;

        public BlockService(
            IAccountBlockRepository accountBlockRepository,
            IAccountRepository accountRepository,
            IFollowRepository followRepository,
            IFollowRequestRepository followRequestRepository,
            INotificationService notificationService,
            IOnlinePresenceService onlinePresenceService,
            IRealtimeService realtimeService,
            IUnitOfWork unitOfWork)
        {
            _accountBlockRepository = accountBlockRepository;
            _accountRepository = accountRepository;
            _followRepository = followRepository;
            _followRequestRepository = followRequestRepository;
            _notificationService = notificationService;
            _onlinePresenceService = onlinePresenceService;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
        }

        public async Task<BlockStatusResponse> BlockAsync(Guid currentId, Guid targetId, CancellationToken cancellationToken = default)
        {
            if (currentId == Guid.Empty || targetId == Guid.Empty)
            {
                throw new BadRequestException("Current account and target account are required.");
            }

            if (currentId == targetId)
            {
                throw new BadRequestException("You cannot block yourself.");
            }

            var currentAccount = await _accountRepository.GetAccountById(currentId);
            if (currentAccount == null)
            {
                throw new ForbiddenException("You must reactivate your account to manage blocked users.");
            }

            var targetAccount = await _accountRepository.GetAccountById(targetId);
            if (targetAccount == null)
            {
                throw new BadRequestException("This user is unavailable or does not exist.");
            }

            var removedCurrentFollow = false;
            var removedTargetFollow = false;
            var removedCurrentRequest = false;
            var removedTargetRequest = false;
            var currentFollowers = 0;
            var currentFollowing = 0;
            var targetFollowers = 0;
            var targetFollowing = 0;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var eventAt = DateTime.UtcNow;
                await _accountBlockRepository.AddIgnoreExistingAsync(new Domain.Entities.AccountBlock
                {
                    BlockerId = currentId,
                    BlockedId = targetId,
                    BlockerSnapshotUsername = currentAccount.Username,
                    BlockedSnapshotUsername = targetAccount.Username
                }, cancellationToken);

                var removedCurrentFollowCount = await _followRepository.RemoveFollowAsync(currentId, targetId);
                if (removedCurrentFollowCount > 0)
                {
                    removedCurrentFollow = true;
                    await EnqueueFollowDeactivationAsync(targetId, currentId, eventAt);
                }

                var removedTargetFollowCount = await _followRepository.RemoveFollowAsync(targetId, currentId);
                if (removedTargetFollowCount > 0)
                {
                    removedTargetFollow = true;
                    await EnqueueFollowDeactivationAsync(currentId, targetId, eventAt);
                }

                var removedCurrentRequestCount = await _followRequestRepository.RemoveFollowRequestAsync(currentId, targetId);
                if (removedCurrentRequestCount > 0)
                {
                    removedCurrentRequest = true;
                    await EnqueueFollowRequestDeactivationAsync(targetId, currentId, eventAt);
                }

                var removedTargetRequestCount = await _followRequestRepository.RemoveFollowRequestAsync(targetId, currentId);
                if (removedTargetRequestCount > 0)
                {
                    removedTargetRequest = true;
                    await EnqueueFollowRequestDeactivationAsync(currentId, targetId, eventAt);
                }

                await _unitOfWork.CommitAsync();

                if (!removedCurrentFollow &&
                    !removedTargetFollow &&
                    !removedCurrentRequest &&
                    !removedTargetRequest)
                {
                    return true;
                }

                var currentCounts = await _followRepository.GetFollowCountsAsync(currentId);
                var targetCounts = await _followRepository.GetFollowCountsAsync(targetId);
                currentFollowers = currentCounts.Followers;
                currentFollowing = currentCounts.Following;
                targetFollowers = targetCounts.Followers;
                targetFollowing = targetCounts.Following;

                return true;
            });

            if (removedCurrentFollow)
            {
                await _realtimeService.NotifyFollowChangedAsync(
                    currentId,
                    targetId,
                    "unfollow",
                    targetFollowers,
                    targetFollowing,
                    currentFollowers,
                    currentFollowing);
            }
            else if (removedCurrentRequest)
            {
                await _realtimeService.NotifyFollowChangedAsync(
                    currentId,
                    targetId,
                    "follow_request_removed",
                    targetFollowers,
                    targetFollowing,
                    currentFollowers,
                    currentFollowing,
                    "follow_request_discarded");
            }

            if (removedTargetFollow)
            {
                await _realtimeService.NotifyFollowChangedAsync(
                    targetId,
                    currentId,
                    "unfollow",
                    currentFollowers,
                    currentFollowing,
                    targetFollowers,
                    targetFollowing);
            }
            else if (removedTargetRequest)
            {
                await _realtimeService.NotifyFollowChangedAsync(
                    targetId,
                    currentId,
                    "follow_request_removed",
                    currentFollowers,
                    currentFollowing,
                    targetFollowers,
                    targetFollowing,
                    "follow_request_rejected");
            }

            if (removedCurrentRequest)
            {
                await _realtimeService.NotifyFollowRequestQueueChangedAsync(targetId, "remove", currentId);
            }

            if (removedTargetRequest)
            {
                await _realtimeService.NotifyFollowRequestQueueChangedAsync(currentId, "remove", targetId);
            }

            await _onlinePresenceService.NotifyBlockedPairHiddenAsync(
                currentId,
                targetId,
                cancellationToken);

            return await GetStatusAsync(currentId, targetId, cancellationToken);
        }

        public async Task<PagedResponse<BlockedAccountListItemResponse>> GetBlockedAccountsAsync(
            Guid currentId,
            BlockedAccountListRequest request,
            CancellationToken cancellationToken = default)
        {
            var safeRequest = request ?? new BlockedAccountListRequest();
            var (items, totalItems) = await _accountBlockRepository.GetBlockedAccountsAsync(
                currentId,
                safeRequest.Keyword,
                safeRequest.Page,
                safeRequest.PageSize,
                cancellationToken);

            return new PagedResponse<BlockedAccountListItemResponse>(
                items.Select(x => new BlockedAccountListItemResponse
                {
                    AccountId = x.AccountId,
                    Username = x.Username,
                    FullName = x.FullName,
                    AvatarUrl = x.AvatarUrl,
                    BlockedAt = x.BlockedAt
                }),
                safeRequest.Page,
                safeRequest.PageSize,
                totalItems);
        }

        public async Task<BlockStatusResponse> GetStatusAsync(Guid currentId, Guid targetId, CancellationToken cancellationToken = default)
        {
            if (currentId == Guid.Empty || targetId == Guid.Empty)
            {
                return new BlockStatusResponse
                {
                    TargetId = targetId,
                    IsBlockedByCurrentUser = false,
                    IsBlockedByTargetUser = false,
                    IsBlockedEitherWay = false
                };
            }

            var relation = (await _accountBlockRepository.GetRelationsAsync(currentId, new[] { targetId }, cancellationToken))
                .FirstOrDefault();

            return new BlockStatusResponse
            {
                TargetId = targetId,
                IsBlockedByCurrentUser = relation?.IsBlockedByCurrentUser ?? false,
                IsBlockedByTargetUser = relation?.IsBlockedByTargetUser ?? false,
                IsBlockedEitherWay = relation?.IsBlockedEitherWay ?? false
            };
        }

        public async Task<BlockStatusResponse> UnblockAsync(Guid currentId, Guid targetId, CancellationToken cancellationToken = default)
        {
            if (currentId == Guid.Empty || targetId == Guid.Empty)
            {
                throw new BadRequestException("Current account and target account are required.");
            }

            if (currentId == targetId)
            {
                throw new BadRequestException("You cannot unblock yourself.");
            }

            await _accountBlockRepository.RemoveAsync(currentId, targetId);
            return await GetStatusAsync(currentId, targetId, cancellationToken);
        }

        private Task EnqueueFollowDeactivationAsync(Guid recipientId, Guid actorId, DateTime occurredAt)
        {
            return Task.WhenAll(
                _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                {
                    RecipientId = recipientId,
                    Action = NotificationAggregateActionEnum.Deactivate,
                    Type = NotificationTypeEnum.Follow,
                    AggregateKey = NotificationAggregateKeys.Follow(actorId),
                    SourceType = NotificationSourceTypeEnum.FollowRelation,
                    SourceId = actorId,
                    ActorId = actorId,
                    TargetKind = NotificationTargetKindEnum.Account,
                    TargetId = actorId,
                    KeepWhenEmpty = false,
                    OccurredAt = occurredAt
                }),
                _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                {
                    RecipientId = recipientId,
                    Action = NotificationAggregateActionEnum.Deactivate,
                    Type = NotificationTypeEnum.Follow,
                    AggregateKey = NotificationAggregateKeys.FollowAutoAcceptSummary(recipientId),
                    SourceType = NotificationSourceTypeEnum.FollowRelation,
                    SourceId = actorId,
                    ActorId = actorId,
                    TargetKind = NotificationTargetKindEnum.Account,
                    TargetId = actorId,
                    KeepWhenEmpty = false,
                    OccurredAt = occurredAt
                }));
        }

        private Task EnqueueFollowRequestDeactivationAsync(Guid recipientId, Guid requesterId, DateTime occurredAt)
        {
            return _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
            {
                RecipientId = recipientId,
                Action = NotificationAggregateActionEnum.Deactivate,
                Type = NotificationTypeEnum.FollowRequest,
                AggregateKey = NotificationAggregateKeys.FollowRequest(requesterId),
                SourceType = NotificationSourceTypeEnum.FollowRequest,
                SourceId = requesterId,
                ActorId = requesterId,
                TargetKind = NotificationTargetKindEnum.Account,
                TargetId = requesterId,
                KeepWhenEmpty = false,
                OccurredAt = occurredAt
            });
        }

    }
}
