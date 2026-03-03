using FluentAssertions;
using Moq;
using CloudM.Application.Services.PostSaveServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.PostSaves;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class PostSaveServiceTests
    {
        private readonly Mock<IPostSaveRepository> _postSaveRepositoryMock;
        private readonly Mock<IPostRepository> _postRepositoryMock;
        private readonly Mock<IFollowRepository> _followRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly PostSaveService _postSaveService;

        public PostSaveServiceTests()
        {
            _postSaveRepositoryMock = new Mock<IPostSaveRepository>();
            _postRepositoryMock = new Mock<IPostRepository>();
            _followRepositoryMock = new Mock<IFollowRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _postSaveService = new PostSaveService(
                _postSaveRepositoryMock.Object,
                _postRepositoryMock.Object,
                _followRepositoryMock.Object,
                _unitOfWorkMock.Object
            );
        }

        [Fact]
        public async Task SavePostAsync_PostNotFound_ThrowsNotFoundException()
        {
            var currentId = Guid.NewGuid();
            var postId = Guid.NewGuid();

            _postRepositoryMock
                .Setup(x => x.GetPostBasicInfoById(postId))
                .ReturnsAsync((Post?)null);

            var act = () => _postSaveService.SavePostAsync(currentId, postId);

            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("Post not found or unavailable.");
        }

        [Fact]
        public async Task SavePostAsync_PublicPost_UsesIdempotentInsertAndReturnsSaved()
        {
            var currentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = ownerId,
                Privacy = PostPrivacyEnum.Public
            };

            _postRepositoryMock
                .Setup(x => x.GetPostBasicInfoById(postId))
                .ReturnsAsync(post);
            _postSaveRepositoryMock
                .Setup(x => x.TryAddPostSaveAsync(currentId, postId, It.IsAny<DateTime>()))
                .ReturnsAsync(true);

            var result = await _postSaveService.SavePostAsync(currentId, postId);

            result.PostId.Should().Be(postId);
            result.IsSavedByCurrentUser.Should().BeTrue();
            _postSaveRepositoryMock.Verify(
                x => x.TryAddPostSaveAsync(currentId, postId, It.IsAny<DateTime>()),
                Times.Once);
            _postSaveRepositoryMock.Verify(
                x => x.IsPostSavedByCurrentAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
                Times.Never);
        }

        [Fact]
        public async Task SavePostAsync_PrivatePostNotOwner_ThrowsForbiddenException()
        {
            var currentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = ownerId,
                Privacy = PostPrivacyEnum.Private
            };

            _postRepositoryMock
                .Setup(x => x.GetPostBasicInfoById(postId))
                .ReturnsAsync(post);

            var act = () => _postSaveService.SavePostAsync(currentId, postId);

            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You are not allowed to save this post.");
        }

        [Fact]
        public async Task SavePostAsync_FollowOnlyPostNotFollowing_ThrowsForbiddenException()
        {
            var currentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = ownerId,
                Privacy = PostPrivacyEnum.FollowOnly
            };

            _postRepositoryMock
                .Setup(x => x.GetPostBasicInfoById(postId))
                .ReturnsAsync(post);
            _followRepositoryMock
                .Setup(x => x.IsFollowingAsync(currentId, ownerId))
                .ReturnsAsync(false);

            var act = () => _postSaveService.SavePostAsync(currentId, postId);

            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You are not allowed to save this post.");
        }

        [Fact]
        public async Task GetSavedPostsByCursorAsync_WhenMoreThanLimit_ReturnsTrimmedItemsAndHasMore()
        {
            var currentId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var rawItems = Enumerable.Range(0, 13)
                .Select(i => new PostPersonalListModel
                {
                    PostId = Guid.NewGuid(),
                    PostCode = $"P{i}",
                    SavedAt = now.AddMinutes(-i)
                })
                .ToList();

            _postSaveRepositoryMock
                .Setup(x => x.GetSavedPostsByCurrentCursorAsync(currentId, null, null, 13))
                .ReturnsAsync(rawItems);

            var (items, hasMore) = await _postSaveService.GetSavedPostsByCursorAsync(
                currentId,
                null,
                null,
                12);

            items.Should().HaveCount(12);
            hasMore.Should().BeTrue();
            _postSaveRepositoryMock.Verify(
                x => x.GetSavedPostsByCurrentCursorAsync(currentId, null, null, 13),
                Times.Once);
        }

        [Fact]
        public async Task GetSavedPostsByCursorAsync_WhenLimitInvalid_UsesDefaultLimit()
        {
            var currentId = Guid.NewGuid();
            var rawItems = new List<PostPersonalListModel>
            {
                new PostPersonalListModel { PostId = Guid.NewGuid(), SavedAt = DateTime.UtcNow }
            };

            _postSaveRepositoryMock
                .Setup(x => x.GetSavedPostsByCurrentCursorAsync(currentId, null, null, 13))
                .ReturnsAsync(rawItems);

            var (items, hasMore) = await _postSaveService.GetSavedPostsByCursorAsync(
                currentId,
                null,
                null,
                0);

            items.Should().HaveCount(1);
            hasMore.Should().BeFalse();
            _postSaveRepositoryMock.Verify(
                x => x.GetSavedPostsByCurrentCursorAsync(currentId, null, null, 13),
                Times.Once);
        }
    }
}
