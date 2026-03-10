using AutoMapper;
using CloudM.Infrastructure.Models;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.FollowDTOs;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.FollowRequests;
using CloudM.Infrastructure.Repositories.AccountSettingRepos;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Application.Services.NotificationServices;
using CloudM.Application.Services.RealtimeServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.FollowServices
{
    public class FollowService : IFollowService
    {
        private readonly IFollowRepository _followRepository;
        private readonly IFollowRequestRepository _followRequestRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IAccountSettingRepository _accountSettingRepository;
        private readonly IMapper _mapper;
        private readonly INotificationService _notificationService;
        private readonly IRealtimeService _realtimeService;
        private readonly IUnitOfWork _unitOfWork;

        public FollowService(
            IFollowRepository followRepository,
            IFollowRequestRepository followRequestRepository,
            IMapper mapper,
            IAccountRepository accountRepository,
            IAccountSettingRepository accountSettingRepository,
            INotificationService notificationService,
            IRealtimeService realtimeService,
            IUnitOfWork unitOfWork)
        {
            _followRepository = followRepository;
            _followRequestRepository = followRequestRepository;
            _mapper = mapper;
            _accountRepository = accountRepository;
            _accountSettingRepository = accountSettingRepository;
            _notificationService = notificationService;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
        }

        public FollowService(
            IFollowRepository followRepository,
            IFollowRequestRepository followRequestRepository,
            IMapper mapper,
            IAccountRepository accountRepository,
            IAccountSettingRepository accountSettingRepository,
            IRealtimeService realtimeService,
            IUnitOfWork unitOfWork)
            : this(
                followRepository,
                followRequestRepository,
                mapper,
                accountRepository,
                accountSettingRepository,
                NullNotificationService.Instance,
                realtimeService,
                unitOfWork)
        {
        }

        public async Task<FollowCountResponse> FollowAsync(Guid followerId, Guid targetId)
        {
            if (followerId == targetId)
                throw new BadRequestException("You cannot follow yourself.");

            // Use the optimized IsAccountIdExist which already checks for existence and Active status
            if (!await _accountRepository.IsAccountIdExist(targetId))
                throw new BadRequestException("This user is unavailable or does not exist.");

            if (!await _accountRepository.IsAccountIdExist(followerId))
                throw new ForbiddenException("You must reactivate your account to follow users.");

            var recordExists = await _followRepository.IsFollowRecordExistAsync(followerId, targetId);
            if (recordExists)
                throw new BadRequestException("You already follow this user.");

            var targetFollowPrivacy = await GetTargetFollowPrivacyAsync(targetId);

            if (targetFollowPrivacy == FollowPrivacyEnum.Private)
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var insertedRequest = await _followRequestRepository.AddFollowRequestIgnoreExistingAsync(new FollowRequest
                    {
                        RequesterId = followerId,
                        TargetId = targetId
                    });
                    if (!insertedRequest)
                    {
                        throw new BadRequestException("You already sent a follow request to this user.");
                    }

                    await _unitOfWork.CommitAsync();

                    var targetCounts = await _followRepository.GetFollowCountsAsync(targetId);
                    var myCounts = await _followRepository.GetFollowCountsAsync(followerId);

                    await _realtimeService.NotifyFollowChangedAsync(
                        followerId,
                        targetId,
                        "follow_request",
                        targetCounts.Followers,
                        targetCounts.Following,
                        myCounts.Followers,
                        myCounts.Following,
                        "follow_request_sent");

                    await _realtimeService.NotifyFollowRequestQueueChangedAsync(targetId, "upsert", followerId);

                    return BuildFollowCountResponse(
                        targetCounts,
                        isFollowing: false,
                        isFollowRequestPending: true,
                        targetFollowPrivacy: targetFollowPrivacy);
                });
            }

            var mutation = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var eventAt = DateTime.UtcNow;
                var removedRequestCount = await _followRequestRepository.RemoveFollowRequestAsync(followerId, targetId);
                if (removedRequestCount > 0)
                {
                    await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                    {
                        RecipientId = targetId,
                        Action = NotificationAggregateActionEnum.Deactivate,
                        Type = NotificationTypeEnum.FollowRequest,
                        AggregateKey = NotificationAggregateKeys.FollowRequest(followerId),
                        SourceType = NotificationSourceTypeEnum.FollowRequest,
                        SourceId = followerId,
                        ActorId = followerId,
                        TargetKind = NotificationTargetKindEnum.Account,
                        TargetId = followerId,
                        KeepWhenEmpty = false,
                        OccurredAt = eventAt
                    });
                }

                var insertedFollow = await _followRepository.AddFollowIgnoreExistingAsync(new Follow
                {
                    FollowerId = followerId,
                    FollowedId = targetId
                });

                if (insertedFollow)
                {
                    await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                    {
                        RecipientId = targetId,
                        Action = NotificationAggregateActionEnum.Upsert,
                        Type = NotificationTypeEnum.Follow,
                        AggregateKey = NotificationAggregateKeys.Follow(followerId),
                        SourceType = NotificationSourceTypeEnum.FollowRelation,
                        SourceId = followerId,
                        ActorId = followerId,
                        TargetKind = NotificationTargetKindEnum.Account,
                        TargetId = followerId,
                        KeepWhenEmpty = false,
                        OccurredAt = eventAt
                    });
                }

                await _unitOfWork.CommitAsync();

                var targetCounts = await _followRepository.GetFollowCountsAsync(targetId);
                var myCounts = await _followRepository.GetFollowCountsAsync(followerId);

                return (
                    ShouldNotifyFollowChanged: insertedFollow,
                    RemovedPendingRequest: removedRequestCount > 0,
                    TargetCounts: targetCounts,
                    CurrentCounts: myCounts);
            });

            if (mutation.ShouldNotifyFollowChanged)
            {
                await _realtimeService.NotifyFollowChangedAsync(
                    followerId,
                    targetId,
                    "follow",
                    mutation.TargetCounts.Followers,
                    mutation.TargetCounts.Following,
                    mutation.CurrentCounts.Followers,
                    mutation.CurrentCounts.Following
                );
            }

            if (mutation.RemovedPendingRequest)
            {
                await _realtimeService.NotifyFollowRequestQueueChangedAsync(targetId, "remove", followerId);
            }

            return BuildFollowCountResponse(
                mutation.TargetCounts,
                isFollowing: true,
                isFollowRequestPending: false,
                targetFollowPrivacy: targetFollowPrivacy);
        }

        public async Task<FollowCountResponse> UnfollowAsync(Guid followerId, Guid targetId)
        {
            if (followerId == targetId)
                throw new BadRequestException("You cannot unfollow yourself.");

            var targetFollowPrivacy = await GetTargetFollowPrivacyAsync(targetId);
            var mutation = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var eventAt = DateTime.UtcNow;
                var removedFollowCount = await _followRepository.RemoveFollowAsync(followerId, targetId);
                if (removedFollowCount > 0)
                {
                    var removedRequestCount = await _followRequestRepository.RemoveFollowRequestAsync(followerId, targetId);
                    if (removedRequestCount > 0)
                    {
                        await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                        {
                            RecipientId = targetId,
                            Action = NotificationAggregateActionEnum.Deactivate,
                            Type = NotificationTypeEnum.FollowRequest,
                            AggregateKey = NotificationAggregateKeys.FollowRequest(followerId),
                            SourceType = NotificationSourceTypeEnum.FollowRequest,
                            SourceId = followerId,
                            ActorId = followerId,
                            TargetKind = NotificationTargetKindEnum.Account,
                            TargetId = followerId,
                            KeepWhenEmpty = false,
                            OccurredAt = eventAt
                        });
                    }

                    await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                    {
                        RecipientId = targetId,
                        Action = NotificationAggregateActionEnum.Deactivate,
                        Type = NotificationTypeEnum.Follow,
                        AggregateKey = NotificationAggregateKeys.Follow(followerId),
                        SourceType = NotificationSourceTypeEnum.FollowRelation,
                        SourceId = followerId,
                        ActorId = followerId,
                        TargetKind = NotificationTargetKindEnum.Account,
                        TargetId = followerId,
                        KeepWhenEmpty = false,
                        OccurredAt = eventAt
                    });

                    await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                    {
                        RecipientId = targetId,
                        Action = NotificationAggregateActionEnum.Deactivate,
                        Type = NotificationTypeEnum.Follow,
                        AggregateKey = NotificationAggregateKeys.FollowAutoAcceptSummary(targetId),
                        SourceType = NotificationSourceTypeEnum.FollowRelation,
                        SourceId = followerId,
                        ActorId = followerId,
                        TargetKind = NotificationTargetKindEnum.Account,
                        TargetId = followerId,
                        KeepWhenEmpty = false,
                        OccurredAt = eventAt
                    });

                    await _unitOfWork.CommitAsync();

                    var targetCounts = await _followRepository.GetFollowCountsAsync(targetId);
                    var myCounts = await _followRepository.GetFollowCountsAsync(followerId);

                    return (
                        Action: "unfollow",
                        RemovedPendingRequest: removedRequestCount > 0,
                        TargetCounts: targetCounts,
                        CurrentCounts: myCounts);
                }

                var removedRequestOnlyCount = await _followRequestRepository.RemoveFollowRequestAsync(followerId, targetId);
                if (removedRequestOnlyCount > 0)
                {
                    await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                    {
                        RecipientId = targetId,
                        Action = NotificationAggregateActionEnum.Deactivate,
                        Type = NotificationTypeEnum.FollowRequest,
                        AggregateKey = NotificationAggregateKeys.FollowRequest(followerId),
                        SourceType = NotificationSourceTypeEnum.FollowRequest,
                        SourceId = followerId,
                        ActorId = followerId,
                        TargetKind = NotificationTargetKindEnum.Account,
                        TargetId = followerId,
                        KeepWhenEmpty = false,
                        OccurredAt = eventAt
                    });

                    await _unitOfWork.CommitAsync();

                    var targetCounts = await _followRepository.GetFollowCountsAsync(targetId);
                    var myCounts = await _followRepository.GetFollowCountsAsync(followerId);

                    return (
                        Action: "follow_request_removed",
                        RemovedPendingRequest: true,
                        TargetCounts: targetCounts,
                        CurrentCounts: myCounts);
                }

                throw new BadRequestException("You are not following this user.");
            });

            if (string.Equals(mutation.Action, "unfollow", StringComparison.Ordinal))
            {
                await _realtimeService.NotifyFollowChangedAsync(
                    followerId,
                    targetId,
                    "unfollow",
                    mutation.TargetCounts.Followers,
                    mutation.TargetCounts.Following,
                    mutation.CurrentCounts.Followers,
                    mutation.CurrentCounts.Following
                );
            }
            else
            {
                await _realtimeService.NotifyFollowChangedAsync(
                    followerId,
                    targetId,
                    "follow_request_removed",
                    mutation.TargetCounts.Followers,
                    mutation.TargetCounts.Following,
                    mutation.CurrentCounts.Followers,
                    mutation.CurrentCounts.Following,
                    "follow_request_discarded"
                );
            }

            if (mutation.RemovedPendingRequest)
            {
                await _realtimeService.NotifyFollowRequestQueueChangedAsync(targetId, "remove", followerId);
            }

            return BuildFollowCountResponse(
                mutation.TargetCounts,
                isFollowing: false,
                isFollowRequestPending: false,
                targetFollowPrivacy: targetFollowPrivacy);
        }

        public Task<bool> IsFollowingAsync(Guid followerId, Guid targetId)
        {
            return _followRepository.IsFollowingAsync(followerId, targetId);
        }

        public async Task<FollowCountResponse> GetRelationStatusAsync(Guid currentId, Guid targetId)
        {
            if (!await _accountRepository.IsAccountIdExist(targetId))
                throw new BadRequestException("This user is unavailable or does not exist.");

            var targetCounts = await _followRepository.GetFollowCountsAsync(targetId);
            var isFollowing = await _followRepository.IsFollowRecordExistAsync(currentId, targetId);
            var isFollowRequestPending = !isFollowing && await _followRequestRepository.IsFollowRequestExistAsync(currentId, targetId);
            var targetFollowPrivacy = await GetTargetFollowPrivacyAsync(targetId);

            return BuildFollowCountResponse(
                targetCounts,
                isFollowing,
                isFollowRequestPending,
                targetFollowPrivacy);
        }

        public async Task<FollowRequestCursorResponse> GetPendingRequestsAsync(
            Guid currentId,
            FollowRequestCursorRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!await _accountRepository.IsAccountIdExist(currentId))
            {
                throw new ForbiddenException("You must reactivate your account to manage follow requests.");
            }

            var safeRequest = request ?? new FollowRequestCursorRequest();
            var limit = safeRequest.Limit <= 0 ? 20 : Math.Min(safeRequest.Limit, 50);
            var (items, nextCursorCreatedAt, nextCursorRequesterId) =
                await _followRequestRepository.GetPendingByTargetAsync(
                    currentId,
                    limit,
                    safeRequest.CursorCreatedAt,
                    safeRequest.CursorRequesterId,
                    cancellationToken);
            var unreadSummary = await _notificationService.GetUnreadSummaryAsync(
                currentId,
                cancellationToken);
            var totalCount = unreadSummary.PendingFollowRequestCount;

            return new FollowRequestCursorResponse
            {
                AccountId = currentId,
                Items = items
                    .Select(x => new FollowRequestItemResponse
                    {
                        RequesterId = x.RequesterId,
                        Username = x.Username,
                        FullName = x.FullName,
                        AvatarUrl = x.AvatarUrl,
                        CreatedAt = x.CreatedAt
                    })
                    .ToList(),
                Count = unreadSummary.Count,
                NotificationUnreadCount = unreadSummary.NotificationUnreadCount,
                FollowRequestUnreadCount = unreadSummary.FollowRequestUnreadCount,
                PendingFollowRequestCount = unreadSummary.PendingFollowRequestCount,
                TotalCount = totalCount,
                LastNotificationsSeenAt = unreadSummary.LastNotificationsSeenAt,
                LastFollowRequestsSeenAt = unreadSummary.LastFollowRequestsSeenAt,
                NextCursor = nextCursorCreatedAt.HasValue && nextCursorRequesterId.HasValue
                    ? new FollowRequestNextCursorResponse
                    {
                        CreatedAt = nextCursorCreatedAt.Value,
                        RequesterId = nextCursorRequesterId.Value
                    }
                    : null
            };
        }

        public async Task<PagedResponse<AccountWithFollowStatusModel>> GetSentPendingRequestsAsync(Guid currentId, FollowPagingRequest request)
        {
            if (!await _accountRepository.IsAccountIdExist(currentId))
            {
                throw new ForbiddenException("You must reactivate your account to view sent follow requests.");
            }

            var safeRequest = request ?? new FollowPagingRequest();
            var safePage = safeRequest.Page <= 0 ? 1 : safeRequest.Page;
            var safePageSize = safeRequest.PageSize <= 0 ? 20 : Math.Min(safeRequest.PageSize, 50);
            var (items, total) = await _followRequestRepository.GetPendingSentByRequesterAsync(
                currentId,
                safeRequest.Keyword,
                safeRequest.SortByCreatedASC,
                safePage,
                safePageSize);

            return new PagedResponse<AccountWithFollowStatusModel>
            {
                Items = items,
                TotalItems = total,
                Page = safePage,
                PageSize = safePageSize
            };
        }

        public async Task AcceptFollowRequestAsync(Guid targetId, Guid requesterId)
        {
            if (targetId == requesterId)
                throw new BadRequestException("Invalid follow request.");

            if (!await _accountRepository.IsAccountIdExist(targetId))
                throw new ForbiddenException("You must reactivate your account to accept follow requests.");

            if (!await _accountRepository.IsAccountIdExist(requesterId))
                throw new BadRequestException("This user is unavailable or does not exist.");

            var mutation = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var eventAt = DateTime.UtcNow;
                var removedRequestCount = await _followRequestRepository.RemoveFollowRequestAsync(requesterId, targetId);
                if (removedRequestCount == 0)
                {
                    var followExists = await _followRepository.IsFollowRecordExistAsync(requesterId, targetId);
                    if (followExists)
                    {
                        return (
                            ShouldNotifyAccepted: false,
                            TargetCounts: (Followers: 0, Following: 0),
                            CurrentCounts: (Followers: 0, Following: 0));
                    }

                    throw new BadRequestException("Follow request not found.");
                }

                await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                {
                    RecipientId = targetId,
                    Action = NotificationAggregateActionEnum.Deactivate,
                    Type = NotificationTypeEnum.FollowRequest,
                    AggregateKey = NotificationAggregateKeys.FollowRequest(requesterId),
                    SourceType = NotificationSourceTypeEnum.FollowRequest,
                    SourceId = requesterId,
                    ActorId = requesterId,
                    TargetKind = NotificationTargetKindEnum.Account,
                    TargetId = requesterId,
                    KeepWhenEmpty = false,
                    OccurredAt = eventAt
                });

                var insertedFollow = await _followRepository.AddFollowIgnoreExistingAsync(new Follow
                {
                    FollowerId = requesterId,
                    FollowedId = targetId
                });

                if (insertedFollow)
                {
                    await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                    {
                        RecipientId = targetId,
                        Action = NotificationAggregateActionEnum.Upsert,
                        Type = NotificationTypeEnum.Follow,
                        AggregateKey = NotificationAggregateKeys.Follow(requesterId),
                        SourceType = NotificationSourceTypeEnum.FollowRelation,
                        SourceId = requesterId,
                        ActorId = requesterId,
                        TargetKind = NotificationTargetKindEnum.Account,
                        TargetId = requesterId,
                        KeepWhenEmpty = false,
                        OccurredAt = eventAt
                    });

                    await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                    {
                        RecipientId = requesterId,
                        Action = NotificationAggregateActionEnum.Upsert,
                        Type = NotificationTypeEnum.FollowRequestAccepted,
                        AggregateKey = NotificationAggregateKeys.FollowRequestAccepted(targetId),
                        SourceType = NotificationSourceTypeEnum.FollowRequestAccepted,
                        SourceId = targetId,
                        ActorId = targetId,
                        TargetKind = NotificationTargetKindEnum.Account,
                        TargetId = targetId,
                        KeepWhenEmpty = false,
                        OccurredAt = eventAt
                    });
                }

                await _unitOfWork.CommitAsync();

                var targetCounts = await _followRepository.GetFollowCountsAsync(targetId);
                var requesterCounts = await _followRepository.GetFollowCountsAsync(requesterId);

                return (
                    ShouldNotifyAccepted: insertedFollow,
                    TargetCounts: targetCounts,
                    CurrentCounts: requesterCounts);
            });

            if (!mutation.ShouldNotifyAccepted)
            {
                return;
            }

            await _realtimeService.NotifyFollowChangedAsync(
                requesterId,
                targetId,
                "follow",
                mutation.TargetCounts.Followers,
                mutation.TargetCounts.Following,
                mutation.CurrentCounts.Followers,
                mutation.CurrentCounts.Following,
                "follow_request_accepted");

            await _realtimeService.NotifyFollowRequestQueueChangedAsync(targetId, "remove", requesterId);
        }

        public async Task RemoveFollowRequestAsync(Guid targetId, Guid requesterId)
        {
            if (targetId == requesterId)
                throw new BadRequestException("Invalid follow request.");

            if (!await _accountRepository.IsAccountIdExist(targetId))
                throw new ForbiddenException("You must reactivate your account to manage follow requests.");

            var mutation = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var eventAt = DateTime.UtcNow;
                var removedRequestCount = await _followRequestRepository.RemoveFollowRequestAsync(requesterId, targetId);
                if (removedRequestCount == 0)
                {
                    var followExists = await _followRepository.IsFollowRecordExistAsync(requesterId, targetId);
                    if (followExists)
                    {
                        throw new BadRequestException("Follow request already processed.");
                    }

                    throw new BadRequestException("Follow request not found.");
                }

                await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                {
                    RecipientId = targetId,
                    Action = NotificationAggregateActionEnum.Deactivate,
                    Type = NotificationTypeEnum.FollowRequest,
                    AggregateKey = NotificationAggregateKeys.FollowRequest(requesterId),
                    SourceType = NotificationSourceTypeEnum.FollowRequest,
                    SourceId = requesterId,
                    ActorId = requesterId,
                    TargetKind = NotificationTargetKindEnum.Account,
                    TargetId = requesterId,
                    KeepWhenEmpty = false,
                    OccurredAt = eventAt
                });

                await _unitOfWork.CommitAsync();

                var targetCounts = await _followRepository.GetFollowCountsAsync(targetId);
                var requesterCounts = await _followRepository.GetFollowCountsAsync(requesterId);

                return (
                    TargetCounts: targetCounts,
                    CurrentCounts: requesterCounts);
            });

            await _realtimeService.NotifyFollowChangedAsync(
                requesterId,
                targetId,
                "follow_request_removed",
                mutation.TargetCounts.Followers,
                mutation.TargetCounts.Following,
                mutation.CurrentCounts.Followers,
                mutation.CurrentCounts.Following,
                "follow_request_rejected");

            await _realtimeService.NotifyFollowRequestQueueChangedAsync(targetId, "remove", requesterId);
        }

        public async Task RemoveFollowerAsync(Guid currentId, Guid followerId)
        {
            if (currentId == followerId)
                throw new BadRequestException("Invalid follower removal.");

            if (!await _accountRepository.IsAccountIdExist(currentId))
                throw new ForbiddenException("You must reactivate your account to manage followers.");

            var mutation = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var removedFollowCount = await _followRepository.RemoveFollowAsync(followerId, currentId);
                if (removedFollowCount == 0)
                {
                    return (
                        Removed: false,
                        CurrentCounts: (Followers: 0, Following: 0),
                        FollowerCounts: (Followers: 0, Following: 0));
                }

                var eventAt = DateTime.UtcNow;
                await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                {
                    RecipientId = currentId,
                    Action = NotificationAggregateActionEnum.Deactivate,
                    Type = NotificationTypeEnum.Follow,
                    AggregateKey = NotificationAggregateKeys.Follow(followerId),
                    SourceType = NotificationSourceTypeEnum.FollowRelation,
                    SourceId = followerId,
                    ActorId = followerId,
                    TargetKind = NotificationTargetKindEnum.Account,
                    TargetId = followerId,
                    KeepWhenEmpty = false,
                    OccurredAt = eventAt
                });

                await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                {
                    RecipientId = currentId,
                    Action = NotificationAggregateActionEnum.Deactivate,
                    Type = NotificationTypeEnum.Follow,
                    AggregateKey = NotificationAggregateKeys.FollowAutoAcceptSummary(currentId),
                    SourceType = NotificationSourceTypeEnum.FollowRelation,
                    SourceId = followerId,
                    ActorId = followerId,
                    TargetKind = NotificationTargetKindEnum.Account,
                    TargetId = followerId,
                    KeepWhenEmpty = false,
                    OccurredAt = eventAt
                });

                await _unitOfWork.CommitAsync();

                var currentCounts = await _followRepository.GetFollowCountsAsync(currentId);
                var followerCounts = await _followRepository.GetFollowCountsAsync(followerId);

                return (
                    Removed: true,
                    CurrentCounts: currentCounts,
                    FollowerCounts: followerCounts);
            });

            if (!mutation.Removed)
            {
                return;
            }

            await _realtimeService.NotifyFollowChangedAsync(
                followerId,
                currentId,
                "remove_follower",
                mutation.CurrentCounts.Followers,
                mutation.CurrentCounts.Following,
                mutation.FollowerCounts.Followers,
                mutation.FollowerCounts.Following,
                null);
        }

        public async Task<PagedResponse<AccountWithFollowStatusModel>> GetFollowersAsync(Guid accountId, Guid? currentId, FollowPagingRequest request)
        {
            if (!await _accountRepository.IsAccountIdExist(accountId))
                throw new NotFoundException($"Account with ID {accountId} does not exist.");

            // Privacy Check
            if (currentId != accountId)
            {
                var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(accountId);
                var privacy = settings != null ? settings.FollowerPrivacy : AccountPrivacyEnum.Public;

                if (privacy == AccountPrivacyEnum.Private)
                    throw new ForbiddenException("This user's followers list is private.");

                if (privacy == AccountPrivacyEnum.FollowOnly)
                {
                    bool isFollowing = currentId.HasValue && await _followRepository.IsFollowingAsync(currentId.Value, accountId);
                    if (!isFollowing)
                        throw new ForbiddenException("You must follow this user to see their followers list.");
                }
            }

            var (items, total) = await _followRepository.GetFollowersAsync(accountId, currentId, request.Keyword, request.SortByCreatedASC, request.Page, request.PageSize);

            return new PagedResponse<AccountWithFollowStatusModel>
            {
                Items = items,
                TotalItems = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        public async Task<PagedResponse<AccountWithFollowStatusModel>> GetFollowingAsync(Guid accountId, Guid? currentId, FollowPagingRequest request)
        {
            if (!await _accountRepository.IsAccountIdExist(accountId))
                throw new NotFoundException($"Account with ID {accountId} does not exist.");

            // Privacy Check
            if (currentId != accountId)
            {
                var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(accountId);
                var privacy = settings != null ? settings.FollowingPrivacy : AccountPrivacyEnum.Public;

                if (privacy == AccountPrivacyEnum.Private)
                    throw new ForbiddenException("This user's following list is private.");

                if (privacy == AccountPrivacyEnum.FollowOnly)
                {
                    bool isFollowing = currentId.HasValue && await _followRepository.IsFollowingAsync(currentId.Value, accountId);
                    if (!isFollowing)
                        throw new ForbiddenException("You must follow this user to see their following list.");
                }
            }

            var (items, total) = await _followRepository.GetFollowingAsync(accountId, currentId, request.Keyword, request.SortByCreatedASC, request.Page, request.PageSize);
            return new PagedResponse<AccountWithFollowStatusModel>
            {
                Items = items,
                TotalItems = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        
        public async Task<FollowCountResponse> GetStatsAsync(Guid userId)
        {
             var counts = await _followRepository.GetFollowCountsAsync(userId);
             return BuildFollowCountResponse(
                 counts,
                 isFollowing: false,
                 isFollowRequestPending: false,
                 targetFollowPrivacy: FollowPrivacyEnum.Anyone);
        }

        private async Task<FollowPrivacyEnum> GetTargetFollowPrivacyAsync(Guid targetId)
        {
            var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(targetId);
            return settings?.FollowPrivacy ?? FollowPrivacyEnum.Anyone;
        }

        private static FollowCountResponse BuildFollowCountResponse(
            (int Followers, int Following) counts,
            bool isFollowing,
            bool isFollowRequestPending,
            FollowPrivacyEnum targetFollowPrivacy)
        {
            var normalizedIsRequested = !isFollowing && isFollowRequestPending;

            return new FollowCountResponse
            {
                Followers = counts.Followers,
                Following = counts.Following,
                IsFollowedByCurrentUser = isFollowing,
                IsFollowRequestPendingByCurrentUser = normalizedIsRequested,
                RelationStatus = ResolveRelationStatus(isFollowing, normalizedIsRequested),
                TargetFollowPrivacy = targetFollowPrivacy
            };
        }

        private static FollowRelationStatusEnum ResolveRelationStatus(bool isFollowing, bool isFollowRequestPending)
        {
            if (isFollowing)
            {
                return FollowRelationStatusEnum.Following;
            }

            if (isFollowRequestPending)
            {
                return FollowRelationStatusEnum.Requested;
            }

            return FollowRelationStatusEnum.None;
        }

    }
}
