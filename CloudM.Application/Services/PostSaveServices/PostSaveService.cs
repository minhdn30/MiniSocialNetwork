using CloudM.Application.DTOs.PostDTOs;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.PostSaves;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.PostSaveServices
{
    public class PostSaveService : IPostSaveService
    {
        private readonly IPostSaveRepository _postSaveRepository;
        private readonly IPostRepository _postRepository;
        private readonly IFollowRepository _followRepository;
        private readonly IUnitOfWork _unitOfWork;

        public PostSaveService(
            IPostSaveRepository postSaveRepository,
            IPostRepository postRepository,
            IFollowRepository followRepository,
            IUnitOfWork unitOfWork)
        {
            _postSaveRepository = postSaveRepository;
            _postRepository = postRepository;
            _followRepository = followRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<PostSaveStateResponse> SavePostAsync(Guid currentId, Guid postId)
        {
            var post = await _postRepository.GetPostBasicInfoById(postId);
            if (post == null)
            {
                throw new NotFoundException("Post not found or unavailable.");
            }

            await ValidatePostPrivacyAsync(post, currentId);

            await _postSaveRepository.TryAddPostSaveAsync(currentId, postId, DateTime.UtcNow);

            return new PostSaveStateResponse
            {
                PostId = postId,
                IsSavedByCurrentUser = true
            };
        }

        public async Task<PostSaveStateResponse> UnsavePostAsync(Guid currentId, Guid postId)
        {
            await _postSaveRepository.RemovePostSaveAsync(currentId, postId);
            await _unitOfWork.CommitAsync();

            return new PostSaveStateResponse
            {
                PostId = postId,
                IsSavedByCurrentUser = false
            };
        }

        public async Task<(List<PostPersonalListModel> Items, bool HasMore)> GetSavedPostsByCursorAsync(
            Guid currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit)
        {
            const int defaultLimit = 12;
            const int maxLimit = 50;

            if (limit <= 0)
            {
                limit = defaultLimit;
            }
            else if (limit > maxLimit)
            {
                limit = maxLimit;
            }

            var posts = await _postSaveRepository.GetSavedPostsByCurrentCursorAsync(
                currentId,
                cursorCreatedAt,
                cursorPostId,
                limit + 1);

            var hasMore = posts.Count > limit;
            if (hasMore)
            {
                posts = posts.Take(limit).ToList();
            }

            return (posts, hasMore);
        }

        private async Task ValidatePostPrivacyAsync(Post post, Guid currentId)
        {
            if (post.AccountId == currentId)
            {
                return;
            }

            if (post.Privacy == PostPrivacyEnum.Private)
            {
                throw new ForbiddenException("You are not allowed to save this post.");
            }

            if (post.Privacy == PostPrivacyEnum.FollowOnly)
            {
                var isFollowing = await _followRepository.IsFollowingAsync(currentId, post.AccountId);
                if (!isFollowing)
                {
                    throw new ForbiddenException("You are not allowed to save this post.");
                }
            }
        }
    }
}
