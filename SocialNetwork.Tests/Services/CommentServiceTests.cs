using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Moq;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.Services.CommentServices;
using SocialNetwork.Application.Services.RealtimeServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.CommentReacts;
using SocialNetwork.Infrastructure.Repositories.Comments;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.Posts;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using Xunit;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Tests.Services
{
    public class CommentServiceTests
    {
        private readonly Mock<ICommentRepository> _mockCommentRepo;
        private readonly Mock<ICommentReactRepository> _mockCommentReactRepo;
        private readonly Mock<IPostRepository> _mockPostRepo;
        private readonly Mock<IAccountRepository> _mockAccountRepo;
        private readonly Mock<IFollowRepository> _mockFollowRepo;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IRealtimeService> _mockRealtimeService;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly CommentService _commentService;

        public CommentServiceTests()
        {
            _mockCommentRepo = new Mock<ICommentRepository>();
            _mockCommentReactRepo = new Mock<ICommentReactRepository>();
            _mockPostRepo = new Mock<IPostRepository>();
            _mockAccountRepo = new Mock<IAccountRepository>();
            _mockFollowRepo = new Mock<IFollowRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockRealtimeService = new Mock<IRealtimeService>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();

            _commentService = new CommentService(
                _mockCommentRepo.Object,
                _mockCommentReactRepo.Object,
                _mockPostRepo.Object,
                _mockAccountRepo.Object,
                _mockFollowRepo.Object,
                _mockMapper.Object,
                _mockRealtimeService.Object,
                _mockUnitOfWork.Object
            );
        }

        #region AddCommentAsync Tests

        [Fact]
        public async Task AddCommentAsync_WhenValidRequest_CreatesComment()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var post = new Post { PostId = postId, AccountId = accountId, Privacy = PostPrivacyEnum.Public };
            var account = new Account { AccountId = accountId, Status = AccountStatusEnum.Active };
            var request = new CommentCreateRequest { Content = "Test comment" };
            var comment = new Comment { CommentId = Guid.NewGuid(), Content = "Test comment", PostId = postId, AccountId = accountId };
            var expectedResponse = new CommentResponse { CommentId = comment.CommentId, Content = "Test comment" };

            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync(account);
            _mockMapper.Setup(x => x.Map<Comment>(request)).Returns(comment);
            _mockMapper.Setup(x => x.Map<CommentResponse>(comment)).Returns(expectedResponse);
            _mockMapper.Setup(x => x.Map<AccountBasicInfoResponse>(account)).Returns(new AccountBasicInfoResponse());
            _mockCommentRepo.Setup(x => x.AddComment(It.IsAny<Comment>())).Returns(Task.CompletedTask);
            _mockCommentRepo.Setup(x => x.CountCommentsByPostId(postId)).ReturnsAsync(1);
            _mockUnitOfWork.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<CommentResponse>>>(), It.IsAny<Func<Task>?>()))
                .Returns<Func<Task<CommentResponse>>, Func<Task>?>((func, _) => func());
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockRealtimeService.Setup(x => x.NotifyCommentCreatedAsync(postId, It.IsAny<CommentResponse>(), null))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _commentService.AddCommentAsync(postId, accountId, request);

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be("Test comment");
        }

        [Fact]
        public async Task AddCommentAsync_WhenPostNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync((Post?)null);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _commentService.AddCommentAsync(postId, Guid.NewGuid(), new CommentCreateRequest()));
        }

        [Fact]
        public async Task AddCommentAsync_WhenAccountInactive_ThrowsForbiddenException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var post = new Post { PostId = postId, Privacy = PostPrivacyEnum.Public };
            var account = new Account { AccountId = accountId, Status = AccountStatusEnum.Inactive };

            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync(account);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _commentService.AddCommentAsync(postId, accountId, new CommentCreateRequest()));
        }

        [Fact]
        public async Task AddCommentAsync_WhenReplyToReply_ThrowsBadRequestException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var parentCommentId = Guid.NewGuid();
            var post = new Post { PostId = postId, Privacy = PostPrivacyEnum.Public };
            var account = new Account { AccountId = accountId, Status = AccountStatusEnum.Active };
            var request = new CommentCreateRequest { Content = "Reply", ParentCommentId = parentCommentId };

            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync(account);
            _mockCommentRepo.Setup(x => x.IsCommentExist(parentCommentId)).ReturnsAsync(true);
            _mockCommentRepo.Setup(x => x.IsCommentCanReply(parentCommentId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _commentService.AddCommentAsync(postId, accountId, request));
        }

        #endregion

        #region UpdateCommentAsync Tests

        [Fact]
        public async Task UpdateCommentAsync_WhenOwner_UpdatesSuccessfully()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var comment = new Comment
            {
                CommentId = commentId,
                AccountId = accountId,
                PostId = postId,
                Content = "Old content",
                Account = new Account { Status = AccountStatusEnum.Active }
            };
            var post = new Post { PostId = postId, Privacy = PostPrivacyEnum.Public };
            var request = new CommentUpdateRequest { Content = "Updated content" };
            var expectedResponse = new CommentResponse { CommentId = commentId, Content = "Updated content" };

            _mockCommentRepo.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _mockMapper.Setup(x => x.Map<CommentResponse>(It.IsAny<Comment>())).Returns(expectedResponse);
            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync(new Account());
            _mockMapper.Setup(x => x.Map<AccountBasicInfoResponse>(It.IsAny<Account>())).Returns(new AccountBasicInfoResponse());
            _mockCommentRepo.Setup(x => x.UpdateComment(It.IsAny<Comment>())).Returns(Task.CompletedTask);
            _mockCommentReactRepo.Setup(x => x.CountCommentReactAsync(commentId)).ReturnsAsync(0);
            _mockCommentRepo.Setup(x => x.CountCommentRepliesAsync(commentId)).ReturnsAsync(0);
            _mockCommentRepo.Setup(x => x.CountCommentsByPostId(postId)).ReturnsAsync(1);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockRealtimeService.Setup(x => x.NotifyCommentUpdatedAsync(postId, It.IsAny<CommentResponse>())).Returns(Task.CompletedTask);

            // Act
            var result = await _commentService.UpdateCommentAsync(commentId, accountId, request);

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be("Updated content");
        }

        [Fact]
        public async Task UpdateCommentAsync_WhenNotOwner_ThrowsForbiddenException()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var comment = new Comment { CommentId = commentId, AccountId = ownerId };

            _mockCommentRepo.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _commentService.UpdateCommentAsync(commentId, otherId, new CommentUpdateRequest()));
        }

        [Fact]
        public async Task UpdateCommentAsync_WhenCommentNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            _mockCommentRepo.Setup(x => x.GetCommentById(commentId)).ReturnsAsync((Comment?)null);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _commentService.UpdateCommentAsync(commentId, Guid.NewGuid(), new CommentUpdateRequest()));
        }

        #endregion

        #region DeleteCommentAsync Tests

        [Fact]
        public async Task DeleteCommentAsync_WhenOwner_DeletesSuccessfully()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var comment = new Comment
            {
                CommentId = commentId,
                AccountId = accountId,
                PostId = postId,
                Account = new Account { Status = AccountStatusEnum.Active }
            };
            var post = new Post { PostId = postId, AccountId = Guid.NewGuid(), Privacy = PostPrivacyEnum.Public };

            _mockCommentRepo.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _mockCommentRepo.Setup(x => x.DeleteCommentWithReplies(commentId)).Returns(Task.CompletedTask);
            _mockCommentRepo.Setup(x => x.CountCommentsByPostId(postId)).ReturnsAsync(0);
            _mockUnitOfWork.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<CommentDeleteResult>>>(), It.IsAny<Func<Task>?>()))
                .Returns<Func<Task<CommentDeleteResult>>, Func<Task>?>((func, _) => func());
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockRealtimeService.Setup(x => x.NotifyCommentDeletedAsync(postId, commentId, null, 0, null)).Returns(Task.CompletedTask);

            // Act
            var result = await _commentService.DeleteCommentAsync(commentId, accountId, false);

            // Assert
            result.Should().NotBeNull();
            result.PostId.Should().Be(postId);
        }

        [Fact]
        public async Task DeleteCommentAsync_WhenPostOwner_DeletesSuccessfully()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var commentOwnerId = Guid.NewGuid();
            var postOwnerId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var comment = new Comment
            {
                CommentId = commentId,
                AccountId = commentOwnerId,
                PostId = postId,
                Account = new Account { Status = AccountStatusEnum.Active }
            };
            var post = new Post { PostId = postId, AccountId = postOwnerId, Privacy = PostPrivacyEnum.Public };

            _mockCommentRepo.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _mockCommentRepo.Setup(x => x.DeleteCommentWithReplies(commentId)).Returns(Task.CompletedTask);
            _mockCommentRepo.Setup(x => x.CountCommentsByPostId(postId)).ReturnsAsync(0);
            _mockUnitOfWork.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<CommentDeleteResult>>>(), It.IsAny<Func<Task>?>()))
                .Returns<Func<Task<CommentDeleteResult>>, Func<Task>?>((func, _) => func());
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockRealtimeService.Setup(x => x.NotifyCommentDeletedAsync(postId, commentId, null, 0, null)).Returns(Task.CompletedTask);

            // Act
            var result = await _commentService.DeleteCommentAsync(commentId, postOwnerId, false);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteCommentAsync_WhenNotOwnerOrPostOwner_ThrowsForbiddenException()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var commentOwnerId = Guid.NewGuid();
            var postOwnerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var comment = new Comment
            {
                CommentId = commentId,
                AccountId = commentOwnerId,
                PostId = postId,
                Account = new Account { Status = AccountStatusEnum.Inactive }
            };
            var post = new Post { PostId = postId, AccountId = postOwnerId };

            _mockCommentRepo.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _commentService.DeleteCommentAsync(commentId, otherId, false));
        }

        #endregion

        #region GetCommentsByPostIdAsync Tests

        [Fact]
        public async Task GetCommentsByPostIdAsync_WhenPostExists_ReturnsPagedComments()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var post = new Post { PostId = postId, Privacy = PostPrivacyEnum.Public };
            var comments = new List<CommentWithReplyCountModel>
            {
                new CommentWithReplyCountModel { CommentId = Guid.NewGuid(), Owner = new AccountBasicInfoModel { AccountId = currentId }, PostOwnerId = Guid.NewGuid() }
            };

            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _mockCommentRepo.Setup(x => x.GetCommentsByPostIdWithReplyCountAsync(postId, currentId, 1, 10))
                .Returns(Task.FromResult<(IEnumerable<CommentWithReplyCountModel> items, int totalItems)>((comments, 1)));
            _mockMapper.Setup(x => x.Map<CommentResponse>(It.IsAny<CommentWithReplyCountModel>()))
                .Returns(new CommentResponse());

            // Act
            var result = await _commentService.GetCommentsByPostIdAsync(postId, currentId, 1, 10);

            // Assert
            result.Should().NotBeNull();
            result.TotalItems.Should().Be(1);
        }

        [Fact]
        public async Task GetCommentsByPostIdAsync_WhenPostNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync((Post?)null);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _commentService.GetCommentsByPostIdAsync(postId, null, 1, 10));
        }

        #endregion

        #region GetRepliesByCommentIdAsync Tests

        [Fact]
        public async Task GetRepliesByCommentIdAsync_WhenCommentExists_ReturnsReplies()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var comment = new Comment { CommentId = commentId, PostId = postId };
            var post = new Post { PostId = postId, Privacy = PostPrivacyEnum.Public };
            var replies = new List<ReplyCommentModel>();

            _mockCommentRepo.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _mockCommentRepo.Setup(x => x.GetRepliesByCommentIdAsync(commentId, currentId, 1, 10))
                .Returns(Task.FromResult<(IEnumerable<ReplyCommentModel> items, int totalItems)>((replies, 0)));

            // Act
            var result = await _commentService.GetRepliesByCommentIdAsync(commentId, currentId, 1, 10);

            // Assert
            result.Should().NotBeNull();
            result.TotalItems.Should().Be(0);
        }

        [Fact]
        public async Task GetRepliesByCommentIdAsync_WhenCommentNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            _mockCommentRepo.Setup(x => x.GetCommentById(commentId)).ReturnsAsync((Comment?)null);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _commentService.GetRepliesByCommentIdAsync(commentId, null, 1, 10));
        }

        #endregion

        #region Helper Method Tests

        [Fact]
        public async Task GetCommentByIdAsync_WhenExists_ReturnsComment()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var comment = new Comment { CommentId = commentId };
            var expectedResponse = new CommentResponse { CommentId = commentId };

            _mockCommentRepo.Setup(x => x.GetCommentById(commentId)).ReturnsAsync(comment);
            _mockMapper.Setup(x => x.Map<CommentResponse>(comment)).Returns(expectedResponse);

            // Act
            var result = await _commentService.GetCommentByIdAsync(commentId);

            // Assert
            result.Should().NotBeNull();
            result!.CommentId.Should().Be(commentId);
        }

        [Fact]
        public async Task GetCommentByIdAsync_WhenNotExists_ReturnsNull()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            _mockCommentRepo.Setup(x => x.GetCommentById(commentId)).ReturnsAsync((Comment?)null);

            // Act
            var result = await _commentService.GetCommentByIdAsync(commentId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetReplyCountAsync_ReturnsCount()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            _mockCommentRepo.Setup(x => x.CountCommentRepliesAsync(commentId)).ReturnsAsync(5);

            // Act
            var result = await _commentService.GetReplyCountAsync(commentId);

            // Assert
            result.Should().Be(5);
        }

        #endregion
    }
}
