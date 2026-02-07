using AutoMapper;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostReactDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Comments;
using SocialNetwork.Infrastructure.Repositories.PostReacts;
using SocialNetwork.Infrastructure.Repositories.Posts;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Application.Services.RealtimeServices;
using static SocialNetwork.Application.Exceptions.CustomExceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.PostReactServices
{
    public class PostReactService : IPostReactService
    {
        private readonly IPostReactRepository _postReactRepository;
        private readonly ICommentRepository _commentRepository;
        private readonly IPostRepository _postRepository;
        private readonly IFollowRepository _followRepository;
        private readonly IMapper _mapper;
        private readonly IRealtimeService _realtimeService;

        public PostReactService(IPostReactRepository postReactRepository, ICommentRepository commentRepository, 
            IPostRepository postRepository, IFollowRepository followRepository, IMapper mapper, IRealtimeService realtimeService)
        {
            _postReactRepository = postReactRepository;
            _commentRepository = commentRepository;
            _postRepository = postRepository;
            _followRepository = followRepository;
            _mapper = mapper;
            _realtimeService = realtimeService;
        }
        public async Task<ReactToggleResponse> ToggleReactOnPost(Guid postId, Guid accountId)
        {
            var post = await _postRepository.GetPostBasicInfoById(postId);
            if (post == null)
            {
                throw new BadRequestException($"Post with ID {postId} not found.");
            }

            await ValidatePostPrivacyAsync(post, accountId, "react to");

            var existingReact = await _postReactRepository.GetUserReactOnPostAsync(postId, accountId);
            var isReactedByCurrentUser = false;
            if (existingReact != null)
            {
                await _postReactRepository.RemovePostReact(existingReact);
                isReactedByCurrentUser = false;
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
            }
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
