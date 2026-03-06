using CloudM.Application.DTOs.PostDTOs;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.PostTags;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.PostTagServices
{
    public class PostTagService : IPostTagService
    {
        private const int TagPreviewLimit = 2;

        private readonly IPostRepository _postRepository;
        private readonly IPostTagRepository _postTagRepository;
        private readonly IUnitOfWork _unitOfWork;

        public PostTagService(
            IPostRepository postRepository,
            IPostTagRepository postTagRepository,
            IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository;
            _postTagRepository = postTagRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<PostTagSummaryResponse> UntagMeFromPost(Guid postId, Guid currentId)
        {
            var visibleTaggedAccounts = await _postRepository.GetTaggedAccountsByPostIdAsync(postId, currentId);
            if (visibleTaggedAccounts == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found or has been deleted.");
            }

            var removed = await _postTagRepository.RemoveCurrentUserTagAsync(postId, currentId);
            if (!removed)
            {
                return BuildPostTagSummary(postId, currentId, visibleTaggedAccounts);
            }

            await _unitOfWork.CommitAsync();

            var latestTaggedAccounts = await _postRepository.GetTaggedAccountsByPostIdAsync(postId, currentId);
            if (latestTaggedAccounts != null)
            {
                return BuildPostTagSummary(postId, currentId, latestTaggedAccounts);
            }

            var fallbackTaggedAccounts = visibleTaggedAccounts
                .Where(x => x.AccountId != currentId)
                .ToList();
            return BuildPostTagSummary(postId, currentId, fallbackTaggedAccounts);
        }

        private static PostTagSummaryResponse BuildPostTagSummary(
            Guid postId,
            Guid currentId,
            IEnumerable<PostTaggedAccountModel> taggedAccounts)
        {
            var taggedAccountResponses = taggedAccounts
                .Select(x => new PostTaggedAccountResponse
                {
                    AccountId = x.AccountId,
                    Username = x.Username,
                    FullName = x.FullName,
                    AvatarUrl = x.AvatarUrl,
                    IsFollowing = x.IsFollowing,
                    IsFollower = x.IsFollower
                })
                .ToList();

            return new PostTagSummaryResponse
            {
                PostId = postId,
                TaggedAccountsPreview = taggedAccountResponses
                    .Take(TagPreviewLimit)
                    .ToList(),
                TotalTaggedAccounts = taggedAccountResponses.Count,
                IsCurrentUserTagged = taggedAccountResponses.Any(x => x.AccountId == currentId)
            };
        }
    }
}
