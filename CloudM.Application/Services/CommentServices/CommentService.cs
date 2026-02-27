using AutoMapper;
using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.CommentDTOs;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.Helpers.StoryHelpers;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.CommentReacts;
using CloudM.Infrastructure.Repositories.Comments;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Application.Services.StoryViewServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.CommentServices
{
    public class CommentService : ICommentService
    {
        private readonly ICommentRepository _commentRepository;
        private readonly ICommentReactRepository _commentReactRepository;
        private readonly IPostRepository _postRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IFollowRepository _followRepository;
        private readonly IMapper _mapper;
        private readonly IRealtimeService _realtimeService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStoryRingStateHelper _storyRingStateHelper;

        public CommentService(ICommentRepository commentRepository, ICommentReactRepository commentReactRepository, IPostRepository postRepository,
            IAccountRepository accountRepository, IFollowRepository followRepository, IMapper mapper, IRealtimeService realtimeService,
            IUnitOfWork unitOfWork, IStoryViewService? storyViewService = null, IStoryRingStateHelper? storyRingStateHelper = null)
        {
            _commentRepository = commentRepository;
            _commentReactRepository = commentReactRepository;
            _postRepository = postRepository;
            _accountRepository = accountRepository;
            _followRepository = followRepository;
            _mapper = mapper;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
            _storyRingStateHelper = storyRingStateHelper ?? new StoryRingStateHelper(storyViewService);
        }

        public async Task<CommentResponse> AddCommentAsync(Guid postId, Guid accountId, CommentCreateRequest request)
        {
            var post = await _postRepository.GetPostBasicInfoById(postId);
            if (post == null)
            {
                throw new BadRequestException($"Post with ID {postId} not found.");
            }

            await ValidatePostPrivacyAsync(post, accountId, "comment on");
 
            var account = await _accountRepository.GetAccountById(accountId);
            if(account == null)
            {
                throw new BadRequestException($"Account with ID {accountId} not found.");
            }

            if (account.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to comment.");
            if (request.ParentCommentId.HasValue)
            {
                var parentId = request.ParentCommentId.Value;

                if (!await _commentRepository.IsCommentExist(parentId) ||
                    !await _commentRepository.IsCommentCanReply(parentId))
                {
                    var message = !await _commentRepository.IsCommentExist(parentId)
                        ? $"Parent comment with ID {parentId} not found."
                        : "Cannot reply to a reply. Only one level of reply is allowed.";

                    throw new BadRequestException(message);
                }
            }

            var comment = _mapper.Map<Comment>(request);
            comment.PostId = postId;
            comment.AccountId = accountId;

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _commentRepository.AddComment(comment);
                await _unitOfWork.CommitAsync(); // Physical save needed for IDs and Counts

                var result = _mapper.Map<CommentResponse>(comment);

                // Populate Owner info for realtime rendering
                if (account != null)
                {
                    result.Owner = _mapper.Map<AccountBasicInfoResponse>(account);
                    result.Owner.StoryRingState = await ResolveStoryRingStateAsync(accountId, accountId);
                }

                result.TotalCommentCount = await _commentRepository.CountCommentsByPostId(postId);

                // Calculate business rules
                result.CanEdit = true; // Newly created comment by the user
                result.CanDelete = true; // Owner of the comment can always delete it

                // Send realtime notification
                int? parentReplyCount = null;
                if (result.ParentCommentId.HasValue)
                {
                    parentReplyCount = await _commentRepository.CountCommentRepliesAsync(result.ParentCommentId.Value);
                }
                await _realtimeService.NotifyCommentCreatedAsync(postId, result, parentReplyCount);

                return result;
            });
        }

        public async Task<CommentResponse> UpdateCommentAsync(Guid commentId, Guid accountId, CommentUpdateRequest request)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            if (comment == null)
            {
                throw new BadRequestException($"Comment with ID {commentId} not found.");
            }

            if (comment.AccountId != accountId)
            {
                throw new ForbiddenException("You are not authorized to update this comment.");
            }

            if (comment.Account.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to update comments.");

            var post = await _postRepository.GetPostBasicInfoById(comment.PostId);
            if (post == null)
            {
                throw new BadRequestException("Post no longer exists.");
            }

            await ValidatePostPrivacyAsync(post, accountId, "modify comments on");

            // Only update content as requested
            comment.Content = request.Content;
            comment.UpdatedAt = DateTime.UtcNow;

            await _commentRepository.UpdateComment(comment);
            await _unitOfWork.CommitAsync();
            
            var result = _mapper.Map<CommentResponse>(comment);
            
            // Populate essential info for realtime rendering (consistent with AddCommentAsync)
            var account = await _accountRepository.GetAccountById(accountId);
            if (account != null)
            {
                result.Owner = _mapper.Map<AccountBasicInfoResponse>(account);
                result.Owner.StoryRingState = await ResolveStoryRingStateAsync(accountId, accountId);
            }

            result.ReactCount = await _commentReactRepository.CountCommentReactAsync(comment.CommentId);
            result.ReplyCount = await _commentRepository.CountCommentRepliesAsync(comment.CommentId);
            result.TotalCommentCount = await _commentRepository.CountCommentsByPostId(comment.PostId);
            
            // Calculate business rules
            result.CanEdit = true;
            result.CanDelete = true;

            // Send realtime notification
            await _realtimeService.NotifyCommentUpdatedAsync(comment.PostId, result);

            return result;
        }

        public async Task<CommentDeleteResult> DeleteCommentAsync(Guid commentId,  Guid accountId, bool isAdmin)
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

            bool isPostOwner = post.AccountId == accountId;

            if (comment.AccountId != accountId && !isPostOwner && !isAdmin)
            {
                throw new ForbiddenException("You are not authorized to delete this comment.");
            }

            if (!isAdmin && comment.Account.Status != AccountStatusEnum.Active && !isPostOwner)
                throw new ForbiddenException("You must reactivate your account to delete comments.");

            if (!isAdmin && !isPostOwner)
            {
                await ValidatePostPrivacyAsync(post, accountId, "manage comments on");
            }

            var postId = comment.PostId;
            var parentId = comment.ParentCommentId;

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _commentRepository.DeleteCommentWithReplies(commentId);
                
                // Commit changes first
                await _unitOfWork.CommitAsync();

                // Get updated counts logic
                int? totalComments = null;
                int? parentReplyCount = null;

                if (parentId.HasValue)
                {
                    parentReplyCount = await _commentRepository.CountCommentRepliesAsync(parentId.Value);
                }
                else
                {
                    totalComments = await _commentRepository.CountCommentsByPostId(postId);
                }

                var deleteResult = new CommentDeleteResult
                {
                    PostId = postId,
                    ParentCommentId = parentId,
                    TotalPostComments = totalComments,
                    ParentReplyCount = parentReplyCount
                };

                // Send realtime notification
                await _realtimeService.NotifyCommentDeletedAsync(postId, commentId, parentId, totalComments, parentReplyCount);

                return deleteResult;
            });
        }

        public async Task<PagedResponse<CommentResponse>> GetCommentsByPostIdAsync(Guid postId, Guid? currentId, int page, int pageSize)
        {
            var post = await _postRepository.GetPostBasicInfoById(postId);
            if (post == null)
                throw new BadRequestException($"Post with ID {postId} not found.");

            await ValidatePostPrivacyAsync(post, currentId, "view comments on");

            var (items, totalItems) = await _commentRepository.GetCommentsByPostIdWithReplyCountAsync(postId, currentId, page, pageSize);

            var responseItems = items.Select(item =>
            {
                var response = _mapper.Map<CommentResponse>(item);
                response.CanEdit = currentId != null && item.Owner.AccountId == currentId;
                response.CanDelete = currentId != null && (item.Owner.AccountId == currentId || item.PostOwnerId == currentId);
                return response;
            }).ToList();

            await ApplyStoryRingStatesForCommentOwnersAsync(currentId, responseItems);

            return new PagedResponse<CommentResponse>
            {
                Items = responseItems,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            };
        }
        public async Task<PagedResponse<CommentResponse>> GetRepliesByCommentIdAsync(Guid commentId, Guid? currentId, int page, int pageSize)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            if (comment == null)
                throw new BadRequestException($"Comment with ID {commentId} not found.");

            var post = await _postRepository.GetPostBasicInfoById(comment.PostId);
            if (post == null)
                throw new BadRequestException("Post no longer exists.");

            await ValidatePostPrivacyAsync(post, currentId, "view replies on");

            var (items, totalItems) = await _commentRepository.GetRepliesByCommentIdAsync(commentId, currentId, page, pageSize);

            var responseItems = items.Select(item =>
            {
                var response = _mapper.Map<CommentResponse>(item);
                response.CanEdit = currentId != null && item.Owner.AccountId == currentId;
                response.CanDelete = currentId != null && (item.Owner.AccountId == currentId || item.PostOwnerId == currentId);
                return response;
            }).ToList();

            await ApplyStoryRingStatesForCommentOwnersAsync(currentId, responseItems);

            return new PagedResponse<CommentResponse>
            {
                Items = responseItems,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            };
        }
        public async Task<CommentResponse?> GetCommentByIdAsync(Guid commentId)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            return comment != null ? _mapper.Map<CommentResponse>(comment) : null;
        }

        public async Task<int> GetReplyCountAsync(Guid commentId)
        {
            return await _commentRepository.CountCommentRepliesAsync(commentId);
        }

        private async Task ValidatePostPrivacyAsync(Post post, Guid? currentId, string action)
        {
            if (post.Privacy == PostPrivacyEnum.Private)
            {
                if (currentId == null || currentId != post.AccountId)
                    throw new ForbiddenException($"Only the post owner can {action} a private post.");
            }
            else if (post.Privacy == PostPrivacyEnum.FollowOnly)
            {
                if (currentId == null || (currentId != post.AccountId && !await _followRepository.IsFollowingAsync(currentId.Value, post.AccountId)))
                    throw new ForbiddenException($"Only followers can {action} this post.");
            }
        }

        private async Task ApplyStoryRingStatesForCommentOwnersAsync(Guid? currentId, List<CommentResponse> comments)
        {
            if (!currentId.HasValue || comments.Count == 0)
            {
                return;
            }

            var ownerIds = comments
                .Select(x => x.Owner?.AccountId ?? Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (ownerIds.Count == 0)
            {
                return;
            }

            var stateMap = await _storyRingStateHelper.ResolveManyAsync(currentId.Value, ownerIds);

            foreach (var comment in comments)
            {
                if (comment.Owner == null)
                {
                    continue;
                }

                comment.Owner.StoryRingState = stateMap.TryGetValue(comment.Owner.AccountId, out var ringState)
                    ? ringState
                    : StoryRingStateEnum.None;
            }
        }

        private async Task<StoryRingStateEnum> ResolveStoryRingStateAsync(Guid currentId, Guid targetAccountId)
        {
            return await _storyRingStateHelper.ResolveAsync(currentId, targetAccountId);
        }
    }
}
