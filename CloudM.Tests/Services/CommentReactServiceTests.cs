using AutoMapper;
using FluentAssertions;
using Moq;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.PostReactDTOs;
using CloudM.Application.Services.CommentReactServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.CommentReacts;
using CloudM.Infrastructure.Repositories.Comments;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Tests.Helpers;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class CommentReactServiceTests
    {
        private readonly Mock<ICommentRepository> _commentRepositoryMock;
        private readonly Mock<ICommentReactRepository> _commentReactRepositoryMock;
        private readonly Mock<IPostRepository> _postRepositoryMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IFollowRepository> _followRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IRealtimeService> _realtimeServiceMock;
        private readonly CommentReactService _commentReactService;

        public CommentReactServiceTests()
        {
            _commentRepositoryMock = new Mock<ICommentRepository>();
            _commentReactRepositoryMock = new Mock<ICommentReactRepository>();
            _postRepositoryMock = new Mock<IPostRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _followRepositoryMock = new Mock<IFollowRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _mapperMock = new Mock<IMapper>();
            _realtimeServiceMock = new Mock<IRealtimeService>();

            _commentReactService = new CommentReactService(
                _commentRepositoryMock.Object,
                _commentReactRepositoryMock.Object,
                _postRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _followRepositoryMock.Object,
                _mapperMock.Object,
                _realtimeServiceMock.Object,
                _unitOfWorkMock.Object
            );
        }

        #region ToggleReactOnComment Tests

        [Fact]
        public async Task ToggleReactOnComment_NotReacted_AddsReactAndReturnsResponse()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var comment = TestDataFactory.CreateComment(commentId: commentId, postId: postId);
            var post = TestDataFactory.CreatePost(postId: postId, privacy: PostPrivacyEnum.Public);

            _commentRepositoryMock.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _commentReactRepositoryMock.Setup(x => x.GetUserReactOnCommentAsync(commentId, accountId)).ReturnsAsync((CommentReact?)null);
            _unitOfWorkMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _commentReactRepositoryMock.Setup(x => x.GetReactCountByCommentId(commentId)).ReturnsAsync(1);
            _realtimeServiceMock.Setup(x => x.NotifyCommentReactUpdatedAsync(postId, commentId, 1)).Returns(Task.CompletedTask);

            // Act
            var result = await _commentReactService.ToggleReactOnComment(commentId, accountId);

            // Assert
            result.Should().NotBeNull();
            result.IsReactedByCurrentUser.Should().BeTrue();
            result.ReactCount.Should().Be(1);
            _commentReactRepositoryMock.Verify(x => x.AddCommentReact(It.IsAny<CommentReact>()), Times.Once);
        }

        [Fact]
        public async Task ToggleReactOnComment_AlreadyReacted_RemovesReactAndReturnsResponse()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var comment = TestDataFactory.CreateComment(commentId: commentId, postId: postId);
            var post = TestDataFactory.CreatePost(postId: postId, privacy: PostPrivacyEnum.Public);
            var existingReact = TestDataFactory.CreateCommentReact(commentId: commentId, accountId: accountId);

            _commentRepositoryMock.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _commentReactRepositoryMock.Setup(x => x.GetUserReactOnCommentAsync(commentId, accountId)).ReturnsAsync(existingReact);
            _unitOfWorkMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _commentReactRepositoryMock.Setup(x => x.GetReactCountByCommentId(commentId)).ReturnsAsync(0);
            _realtimeServiceMock.Setup(x => x.NotifyCommentReactUpdatedAsync(postId, commentId, 0)).Returns(Task.CompletedTask);

            // Act
            var result = await _commentReactService.ToggleReactOnComment(commentId, accountId);

            // Assert
            result.Should().NotBeNull();
            result.IsReactedByCurrentUser.Should().BeFalse();
            result.ReactCount.Should().Be(0);
            _commentReactRepositoryMock.Verify(x => x.RemoveCommentReact(existingReact), Times.Once);
        }

        [Fact]
        public async Task ToggleReactOnComment_CommentNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            _commentRepositoryMock.Setup(x => x.GetCommentById(commentId)).ReturnsAsync((Comment?)null);

            // Act
            var act = () => _commentReactService.ToggleReactOnComment(commentId, accountId);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage($"Comment with ID {commentId} not found.");
        }

        [Fact]
        public async Task ToggleReactOnComment_PostNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var comment = TestDataFactory.CreateComment(commentId: commentId, postId: postId);

            _commentRepositoryMock.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync((Post?)null);

            // Act
            var act = () => _commentReactService.ToggleReactOnComment(commentId, accountId);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task ToggleReactOnComment_PrivatePostNotOwner_ThrowsForbiddenException()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var comment = TestDataFactory.CreateComment(commentId: commentId, postId: postId);
            var post = TestDataFactory.CreatePost(postId: postId, ownerId: ownerId, privacy: PostPrivacyEnum.Private);
            post.AccountId = ownerId;

            _commentRepositoryMock.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);

            // Act
            var act = () => _commentReactService.ToggleReactOnComment(commentId, accountId);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>();
        }

        #endregion

        #region GetAccountsReactOnCommentPaged Tests

        [Fact]
        public async Task GetAccountsReactOnCommentPaged_ValidRequest_ReturnsPagedResponse()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var comment = TestDataFactory.CreateComment(commentId: commentId, postId: postId);
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

            _commentRepositoryMock.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _commentReactRepositoryMock.Setup(x => x.GetAccountsReactOnCommentPaged(commentId, currentId, 1, 10))
                .Returns(Task.FromResult((reacts, 1)));

            // Act
            var result = await _commentReactService.GetAccountsReactOnCommentPaged(commentId, currentId, 1, 10);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.TotalItems.Should().Be(1);
        }

        [Fact]
        public async Task GetAccountsReactOnCommentPaged_CommentNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            _commentRepositoryMock.Setup(x => x.GetCommentById(commentId)).ReturnsAsync((Comment?)null);

            // Act
            var act = () => _commentReactService.GetAccountsReactOnCommentPaged(commentId, Guid.NewGuid(), 1, 10);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task GetAccountsReactOnCommentPaged_NonPublicPostNoAuth_ThrowsForbiddenException()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var comment = TestDataFactory.CreateComment(commentId: commentId, postId: postId);
            var post = TestDataFactory.CreatePost(postId: postId, privacy: PostPrivacyEnum.FollowOnly);

            _commentRepositoryMock.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _postRepositoryMock.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);

            // Act
            var act = () => _commentReactService.GetAccountsReactOnCommentPaged(commentId, null, 1, 10);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>();
        }

        #endregion
    }
}
