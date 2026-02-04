using AutoMapper;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostReactDTOs;
using static SocialNetwork.Application.Exceptions.CustomExceptions;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.CommentReacts;
using SocialNetwork.Infrastructure.Repositories.Comments;
using SocialNetwork.Infrastructure.Repositories.Posts;
using SocialNetwork.Infrastructure.Repositories.Follows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.CommentReactServices
{
    public class CommentReactService : ICommentReactService
    {
        private readonly ICommentRepository _commentRepository;
        private readonly ICommentReactRepository _commentReactRepository;
        private readonly IPostRepository _postRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IFollowRepository _followRepository;
        private readonly IMapper _mapper;
        public CommentReactService(ICommentRepository commentRepository, ICommentReactRepository commentReactRepository, IPostRepository postRepository,
            IAccountRepository accountRepository, IFollowRepository followRepository, IMapper mapper)
        {
            _commentRepository = commentRepository;
            _commentReactRepository = commentReactRepository;
            _postRepository = postRepository;
            _accountRepository = accountRepository;
            _followRepository = followRepository;
            _mapper = mapper;
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

            await ValidatePostPrivacyAsync(post, accountId, "react to comments on");

            var existingReact = await _commentReactRepository.GetUserReactOnCommentAsync(commentId, accountId);
            var isReactedByCurrentUser = false;
            if (existingReact != null)
            {
                await _commentReactRepository.RemoveCommentReact(existingReact);
                isReactedByCurrentUser = false;
            }
            else
            {
                var newReact = new CommentReact
                {
                    CommentId = commentId,
                    AccountId = accountId,
                    ReactType = ReactEnum.Love,
                    CreatedAt = DateTime.UtcNow
                };
                await _commentReactRepository.AddCommentReact(newReact);
                isReactedByCurrentUser = true;
            }
            var reactCount = await _commentReactRepository.GetReactCountByCommentId(commentId);
            return new ReactToggleResponse
            {
                ReactCount = reactCount,
                IsReactedByCurrentUser = isReactedByCurrentUser
            };
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
