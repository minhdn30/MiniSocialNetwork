using AutoMapper;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.PostReactDTOs;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Comments;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Infrastructure.Repositories.PostReacts;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Application.Services.NotificationServices;
using CloudM.Application.Services.RealtimeServices;
using static CloudM.Domain.Exceptions.CustomExceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.PostReactServices
{
    public class PostReactService : IPostReactService
    {
        private readonly IPostReactRepository _postReactRepository;
        private readonly ICommentRepository _commentRepository;
        private readonly IPostRepository _postRepository;
        private readonly IFollowRepository _followRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly INotificationService _notificationService;
        private readonly IRealtimeService _realtimeService;
        private readonly IAccountBlockRepository _accountBlockRepository;

        public PostReactService(IPostReactRepository postReactRepository, ICommentRepository commentRepository, 
            IPostRepository postRepository, IFollowRepository followRepository, IMapper mapper, 
            INotificationService notificationService, IRealtimeService realtimeService, IUnitOfWork unitOfWork,
            IAccountBlockRepository? accountBlockRepository = null)
        {
            _postReactRepository = postReactRepository;
            _commentRepository = commentRepository;
            _postRepository = postRepository;
            _followRepository = followRepository;
            _mapper = mapper;
            _notificationService = notificationService;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
            _accountBlockRepository = accountBlockRepository ?? NullAccountBlockRepository.Instance;
        }

        public PostReactService(
            IPostReactRepository postReactRepository,
            ICommentRepository commentRepository,
            IPostRepository postRepository,
            IFollowRepository followRepository,
            IMapper mapper,
            IRealtimeService realtimeService,
            IUnitOfWork unitOfWork)
            : this(
                postReactRepository,
                commentRepository,
                postRepository,
                followRepository,
                mapper,
                NullNotificationService.Instance,
                realtimeService,
                unitOfWork)
        {
        }
        public async Task<ReactToggleResponse> ToggleReactOnPost(Guid postId, Guid accountId)
        {
            var post = await _postRepository.GetPostBasicInfoById(postId);
            if (post == null)
            {
                throw new BadRequestException($"Post with ID {postId} not found.");
            }

            if (await _accountBlockRepository.IsBlockedEitherWayAsync(accountId, post.AccountId))
                throw new BadRequestException("This content is no longer available.");

            await ValidatePostPrivacyAsync(post, accountId, "react to");

            var existingReact = await _postReactRepository.GetUserReactOnPostAsync(postId, accountId);
            var isReactedByCurrentUser = false;
            if (existingReact != null)
            {
                await _postReactRepository.RemovePostReact(existingReact);
                isReactedByCurrentUser = false;
                if (post.AccountId != accountId)
                {
                    await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                    {
                        RecipientId = post.AccountId,
                        Action = NotificationAggregateActionEnum.Deactivate,
                        Type = NotificationTypeEnum.PostReact,
                        AggregateKey = NotificationAggregateKeys.PostReact(postId),
                        SourceType = NotificationSourceTypeEnum.PostReact,
                        SourceId = accountId,
                        ActorId = accountId,
                        TargetKind = NotificationTargetKindEnum.Post,
                        TargetId = postId,
                        KeepWhenEmpty = false,
                        OccurredAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                var newReact = new PostReact
                {
                    PostId = postId,
                    AccountId = accountId,
                    ReactType = ReactEnum.Love,
                    CreatedAt = DateTime.UtcNow
                };
                await _postReactRepository.AddPostReact(newReact);
                isReactedByCurrentUser = true;
                if (post.AccountId != accountId)
                {
                    await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                    {
                        RecipientId = post.AccountId,
                        Action = NotificationAggregateActionEnum.Upsert,
                        Type = NotificationTypeEnum.PostReact,
                        AggregateKey = NotificationAggregateKeys.PostReact(postId),
                        SourceType = NotificationSourceTypeEnum.PostReact,
                        SourceId = accountId,
                        ActorId = accountId,
                        TargetKind = NotificationTargetKindEnum.Post,
                        TargetId = postId,
                        KeepWhenEmpty = false,
                        OccurredAt = DateTime.UtcNow
                    });
                }
            }

            // Must commit before counting to get accurate count from DB
            await _unitOfWork.CommitAsync();

            var reactCount = await _postReactRepository.GetReactCountByPostId(postId);

            // Send realtime notification
            await _realtimeService.NotifyPostReactUpdatedAsync(postId, reactCount);

            return new ReactToggleResponse
            {
                ReactCount = reactCount,
                IsReactedByCurrentUser = isReactedByCurrentUser
            };
        }
        public async Task<PagedResponse<AccountReactListModel>> GetAccountsReactOnPostPaged(Guid postId, Guid? currentId, int page, int pageSize)
        {
            var post = await _postRepository.GetPostBasicInfoById(postId);
            if (post == null)
            {
                throw new BadRequestException($"Post with ID {postId} not found.");
            }

            if (currentId.HasValue &&
                await _accountBlockRepository.IsBlockedEitherWayAsync(currentId.Value, post.AccountId))
                throw new BadRequestException("This content is no longer available.");

            if (currentId.HasValue)
            {
                await ValidatePostPrivacyAsync(post, currentId.Value, "view reactions on");
            }
            else if (post.Privacy != PostPrivacyEnum.Public)
            {
                throw new ForbiddenException("You must be logged in and authorized to view reactions on this post.");
            }

            var (reacts, totalItems) = await _postReactRepository.GetAccountsReactOnPostPaged(postId, currentId, page, pageSize);
            return new PagedResponse<AccountReactListModel>
            {
                Items = reacts,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
        }

        private async Task ValidatePostPrivacyAsync(Post post, Guid currentId, string action)
        {
            if (post.Privacy == PostPrivacyEnum.Private)
            {
                if (currentId != post.AccountId)
                {
                    throw new ForbiddenException($"Only the post owner can {action} a private post.");
                }
            }
            else if (post.Privacy == PostPrivacyEnum.FollowOnly)
            {
                if (currentId != post.AccountId && !await _followRepository.IsFollowingAsync(currentId, post.AccountId))
                {
                    throw new ForbiddenException($"Only followers can {action} this post.");
                }
            }
        }
    }
}
