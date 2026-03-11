using AutoMapper;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.PostReactDTOs;
using static CloudM.Domain.Exceptions.CustomExceptions;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Infrastructure.Repositories.CommentReacts;
using CloudM.Infrastructure.Repositories.Comments;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Application.Services.NotificationServices;
using CloudM.Application.Services.RealtimeServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.CommentReactServices
{
    public class CommentReactService : ICommentReactService
    {
        private readonly ICommentRepository _commentRepository;
        private readonly ICommentReactRepository _commentReactRepository;
        private readonly IPostRepository _postRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IFollowRepository _followRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly INotificationService _notificationService;
        private readonly IRealtimeService _realtimeService;
        private readonly IAccountBlockRepository _accountBlockRepository;

        public CommentReactService(ICommentRepository commentRepository, ICommentReactRepository commentReactRepository, IPostRepository postRepository,
            IAccountRepository accountRepository, IFollowRepository followRepository, IMapper mapper, 
            INotificationService notificationService, IRealtimeService realtimeService, IUnitOfWork unitOfWork,
            IAccountBlockRepository? accountBlockRepository = null)
        {
            _commentRepository = commentRepository;
            _commentReactRepository = commentReactRepository;
            _postRepository = postRepository;
            _accountRepository = accountRepository;
            _followRepository = followRepository;
            _mapper = mapper;
            _notificationService = notificationService;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
            _accountBlockRepository = accountBlockRepository ?? NullAccountBlockRepository.Instance;
        }
        public CommentReactService(
            ICommentRepository commentRepository,
            ICommentReactRepository commentReactRepository,
            IPostRepository postRepository,
            IAccountRepository accountRepository,
            IFollowRepository followRepository,
            IMapper mapper,
            IRealtimeService realtimeService,
            IUnitOfWork unitOfWork)
            : this(
                commentRepository,
                commentReactRepository,
                postRepository,
                accountRepository,
                followRepository,
                mapper,
                NullNotificationService.Instance,
                realtimeService,
                unitOfWork)
        {
        }
        public async Task<ReactToggleResponse> ToggleReactOnComment(Guid commentId, Guid accountId)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            if (comment == null)
            {
                throw new BadRequestException($"Comment with ID {commentId} not found.");
            }

            var post = await _postRepository.GetPostBasicInfoById(comment.PostId);
            if (post == null)
            {
                throw new BadRequestException($"Post with ID {comment.PostId} not found.");
            }

            if (await _accountBlockRepository.IsBlockedEitherWayAsync(accountId, comment.AccountId) ||
                await _accountBlockRepository.IsBlockedEitherWayAsync(accountId, post.AccountId))
                throw new BadRequestException("This content is no longer available.");

            await ValidatePostPrivacyAsync(post, accountId, "react to comments on");

            var nowUtc = DateTime.UtcNow;
            var existingReact = await _commentReactRepository.GetUserReactOnCommentAsync(commentId, accountId);
            var isReactedByCurrentUser = false;
            if (existingReact != null)
            {
                await _commentReactRepository.RemoveCommentReact(existingReact);
                isReactedByCurrentUser = false;
                await EnqueueCommentReactNotificationAsync(
                    comment,
                    accountId,
                    NotificationAggregateActionEnum.Deactivate,
                    nowUtc);
            }
            else
            {
                var newReact = new CommentReact
                {
                    CommentId = commentId,
                    AccountId = accountId,
                    ReactType = ReactEnum.Love,
                    CreatedAt = nowUtc
                };
                await _commentReactRepository.AddCommentReact(newReact);
                isReactedByCurrentUser = true;
                await EnqueueCommentReactNotificationAsync(
                    comment,
                    accountId,
                    NotificationAggregateActionEnum.Upsert,
                    nowUtc);
            }

            // Must commit before counting
            await _unitOfWork.CommitAsync();

            var reactCount = await _commentReactRepository.GetReactCountByCommentId(commentId);

            // Send realtime notification
            await _realtimeService.NotifyCommentReactUpdatedAsync(comment.PostId, commentId, reactCount);

            return new ReactToggleResponse
            {
                ReactCount = reactCount,
                IsReactedByCurrentUser = isReactedByCurrentUser
            };
        }
        private async Task EnqueueCommentReactNotificationAsync(
            Comment comment,
            Guid actorId,
            NotificationAggregateActionEnum action,
            DateTime occurredAt)
        {
            if (comment.AccountId == Guid.Empty || actorId == Guid.Empty || comment.AccountId == actorId)
            {
                return;
            }

            var isReply = comment.ParentCommentId.HasValue;
            await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
            {
                RecipientId = comment.AccountId,
                Action = action,
                Type = isReply ? NotificationTypeEnum.ReplyReact : NotificationTypeEnum.CommentReact,
                AggregateKey = isReply
                    ? NotificationAggregateKeys.ReplyReact(comment.CommentId)
                    : NotificationAggregateKeys.CommentReact(comment.CommentId),
                SourceType = isReply
                    ? NotificationSourceTypeEnum.ReplyReact
                    : NotificationSourceTypeEnum.CommentReact,
                SourceId = actorId,
                ActorId = actorId,
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = comment.PostId,
                KeepWhenEmpty = false,
                OccurredAt = occurredAt
            });
        }
        public async Task<PagedResponse<AccountReactListModel>> GetAccountsReactOnCommentPaged(Guid commentId, Guid? currentId, int page, int pageSize)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            if (comment == null)
            {
                throw new BadRequestException($"Comment with ID {commentId} not found.");
            }

            var post = await _postRepository.GetPostBasicInfoById(comment.PostId);
            if (post == null)
            {
                throw new BadRequestException($"Post with ID {comment.PostId} not found.");
            }

            if (currentId.HasValue &&
                (await _accountBlockRepository.IsBlockedEitherWayAsync(currentId.Value, comment.AccountId) ||
                 await _accountBlockRepository.IsBlockedEitherWayAsync(currentId.Value, post.AccountId)))
                throw new BadRequestException("This content is no longer available.");

            if (currentId.HasValue)
            {
                await ValidatePostPrivacyAsync(post, currentId.Value, "view comment reactions on");
            }
            else if (post.Privacy != PostPrivacyEnum.Public)
            {
                throw new ForbiddenException("You must be logged in and authorized to view reactions on this post.");
            }

            var (reacts, totalItems) = await _commentReactRepository.GetAccountsReactOnCommentPaged(commentId, currentId, page, pageSize);
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
