using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.CommentReacts;
using SocialNetwork.Infrastructure.Repositories.Comments;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.Posts;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Application.Services.RealtimeServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.CommentServices
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

        public CommentService(ICommentRepository commentRepository, ICommentReactRepository commentReactRepository, IPostRepository postRepository,
            IAccountRepository accountRepository, IFollowRepository followRepository, IMapper mapper, IRealtimeService realtimeService,
            IUnitOfWork unitOfWork)
        {
            _commentRepository = commentRepository;
            _commentReactRepository = commentReactRepository;
            _postRepository = postRepository;
            _accountRepository = accountRepository;
            _followRepository = followRepository;
            _mapper = mapper;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
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

                // Get updated counts logic
                // User requirement: "totalpostcomment will update when deleting a comment, not a reply"
                int? totalComments = null;
                int? parentReplyCount = null;

                if (parentId.HasValue)
                {
                    // Deleting a reply: Update parent's reply count, but NOT total post count
                    parentReplyCount = await _commentRepository.CountCommentRepliesAsync(parentId.Value);
                }
                else
                {
                    // Deleting a main comment: Update total post count
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
    }
}
