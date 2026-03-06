using AutoMapper;
using FluentAssertions;
using Moq;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.PostReactDTOs;
using CloudM.Application.Services.PostReactServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Comments;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.PostReacts;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Tests.Helpers;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class PostReactServiceTests
    {
        private readonly Mock<IPostReactRepository> _postReactRepositoryMock;
        private readonly Mock<ICommentRepository> _commentRepositoryMock;
        private readonly Mock<IPostRepository> _postRepositoryMock;
        private readonly Mock<IFollowRepository> _followRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IRealtimeService> _realtimeServiceMock;
        private readonly PostReactService _postReactService;

        public PostReactServiceTests()
        {
            _postReactRepositoryMock = new Mock<IPostReactRepository>();
            _commentRepositoryMock = new Mock<ICommentRepository>();
            _postRepositoryMock = new Mock<IPostRepository>();
            _followRepositoryMock = new Mock<IFollowRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _mapperMock = new Mock<IMapper>();
            _realtimeServiceMock = new Mock<IRealtimeService>();

            _postReactService = new PostReactService(
                _postReactRepositoryMock.Object,
                _commentRepositoryMock.Object,
                _postRepositoryMock.Object,
                _followRepositoryMock.Object,
                _mapperMock.Object,
                _realtimeServiceMock.Object,
                _unitOfWorkMock.Object
            );
        }

        #region ToggleReactOnPost Tests

        [Fact]
        public async Task ToggleReactOnPost_NotReacted_AddsReactAndReturnsResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var post = TestDataFactory.CreatePost(postId: postId, privacy: PostPrivacyEnum.Public);

            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _postReactRepositoryMock.Setup(x => x.GetUserReactOnPostAsync(postId, accountId)).ReturnsAsync((PostReact?)null);
            _unitOfWorkMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _postReactRepositoryMock.Setup(x => x.GetReactCountByPostId(postId)).ReturnsAsync(1);
            _realtimeServiceMock.Setup(x => x.NotifyPostReactUpdatedAsync(postId, 1)).Returns(Task.CompletedTask);

            // Act
            var result = await _postReactService.ToggleReactOnPost(postId, accountId);

            // Assert
            result.Should().NotBeNull();
            result.IsReactedByCurrentUser.Should().BeTrue();
            result.ReactCount.Should().Be(1);
            _postReactRepositoryMock.Verify(x => x.AddPostReact(It.IsAny<PostReact>()), Times.Once);
        }

        [Fact]
        public async Task ToggleReactOnPost_AlreadyReacted_RemovesReactAndReturnsResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var post = TestDataFactory.CreatePost(postId: postId, privacy: PostPrivacyEnum.Public);
            var existingReact = TestDataFactory.CreatePostReact(postId: postId, accountId: accountId);

            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _postReactRepositoryMock.Setup(x => x.GetUserReactOnPostAsync(postId, accountId)).ReturnsAsync(existingReact);
            _unitOfWorkMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _postReactRepositoryMock.Setup(x => x.GetReactCountByPostId(postId)).ReturnsAsync(0);
            _realtimeServiceMock.Setup(x => x.NotifyPostReactUpdatedAsync(postId, 0)).Returns(Task.CompletedTask);

            // Act
            var result = await _postReactService.ToggleReactOnPost(postId, accountId);

            // Assert
            result.Should().NotBeNull();
            result.IsReactedByCurrentUser.Should().BeFalse();
            result.ReactCount.Should().Be(0);
            _postReactRepositoryMock.Verify(x => x.RemovePostReact(existingReact), Times.Once);
        }

        [Fact]
        public async Task ToggleReactOnPost_PostNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync((Post?)null);

            // Act
            var act = () => _postReactService.ToggleReactOnPost(postId, accountId);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage($"Post with ID {postId} not found.");
        }

        [Fact]
        public async Task ToggleReactOnPost_PrivatePostNotOwner_ThrowsForbiddenException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var accountId = Guid.NewGuid(); // Different from owner
            var post = TestDataFactory.CreatePost(postId: postId, ownerId: ownerId, privacy: PostPrivacyEnum.Private);
            post.AccountId = ownerId;

            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);

            // Act
            var act = () => _postReactService.ToggleReactOnPost(postId, accountId);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>();
        }

        [Fact]
        public async Task ToggleReactOnPost_FollowOnlyPostNotFollowing_ThrowsForbiddenException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var post = TestDataFactory.CreatePost(postId: postId, ownerId: ownerId, privacy: PostPrivacyEnum.FollowOnly);
            post.AccountId = ownerId;

            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _followRepositoryMock.Setup(x => x.IsFollowingAsync(accountId, ownerId)).ReturnsAsync(false);

            // Act
            var act = () => _postReactService.ToggleReactOnPost(postId, accountId);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>();
        }

        #endregion

        #region GetAccountsReactOnPostPaged Tests

        [Fact]
        public async Task GetAccountsReactOnPostPaged_ValidRequest_ReturnsPagedResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var post = TestDataFactory.CreatePost(postId: postId, privacy: PostPrivacyEnum.Public);
            var reacts = new List<AccountReactListModel>
            {
                new AccountReactListModel 
                { 
                    AccountId = Guid.NewGuid(), 
                    FullName = "Test User", 
                    Username = "testuser" 
                }
            };

            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _postReactRepositoryMock.Setup(x => x.GetAccountsReactOnPostPaged(postId, currentId, 1, 10))
                .Returns(Task.FromResult((reacts, 1)));

            // Act
            var result = await _postReactService.GetAccountsReactOnPostPaged(postId, currentId, 1, 10);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.TotalItems.Should().Be(1);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(10);
        }

        [Fact]
        public async Task GetAccountsReactOnPostPaged_PostNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync((Post?)null);

            // Act
            var act = () => _postReactService.GetAccountsReactOnPostPaged(postId, Guid.NewGuid(), 1, 10);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task GetAccountsReactOnPostPaged_NonPublicPostNoAuth_ThrowsForbiddenException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var post = TestDataFactory.CreatePost(postId: postId, privacy: PostPrivacyEnum.FollowOnly);

            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);

            // Act - no currentId (not logged in)
            var act = () => _postReactService.GetAccountsReactOnPostPaged(postId, null, 1, 10);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You must be logged in and authorized to view reactions on this post.");
        }

        #endregion
    }
}
