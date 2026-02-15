using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using SocialNetwork.Application.Services.PostServices;
using SocialNetwork.Application.Services.RealtimeServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.Comments;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.PostMedias;
using SocialNetwork.Infrastructure.Repositories.PostReacts;
using SocialNetwork.Infrastructure.Repositories.Posts;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using Xunit;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Tests.Services
{
    public class PostServiceTests
    {
        private readonly Mock<IPostRepository> _mockPostRepo;
        private readonly Mock<IPostMediaRepository> _mockPostMediaRepo;
        private readonly Mock<IPostReactRepository> _mockPostReactRepo;
        private readonly Mock<ICommentRepository> _mockCommentRepo;
        private readonly Mock<IAccountRepository> _mockAccountRepo;
        private readonly Mock<ICloudinaryService> _mockCloudinaryService;
        private readonly Mock<IFileTypeDetector> _mockFileTypeDetector;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IRealtimeService> _mockRealtimeService;
        private readonly PostService _postService;

        public PostServiceTests()
        {
            _mockPostRepo = new Mock<IPostRepository>();
            _mockPostMediaRepo = new Mock<IPostMediaRepository>();
            _mockPostReactRepo = new Mock<IPostReactRepository>();
            _mockCommentRepo = new Mock<ICommentRepository>();
            _mockAccountRepo = new Mock<IAccountRepository>();
            _mockCloudinaryService = new Mock<ICloudinaryService>();
            _mockFileTypeDetector = new Mock<IFileTypeDetector>();
            _mockMapper = new Mock<IMapper>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRealtimeService = new Mock<IRealtimeService>();

            _postService = new PostService(
                _mockPostReactRepo.Object,
                _mockPostMediaRepo.Object,
                _mockPostRepo.Object,
                _mockCommentRepo.Object,
                _mockAccountRepo.Object,
                _mockCloudinaryService.Object,
                _mockFileTypeDetector.Object,
                _mockMapper.Object,
                _mockUnitOfWork.Object,
                _mockRealtimeService.Object
            );
        }

        #region GetPostById Tests

        [Fact]
        public async Task GetPostById_WhenPostExists_ReturnsPostDetail()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                Content = "Test post",
                AccountId = currentId
            };

            var expectedResponse = new PostDetailResponse
            {
                PostId = postId,
                Content = "Test post"
            };

            _mockPostRepo.Setup(x => x.GetPostById(postId)).ReturnsAsync(post);
            _mockMapper.Setup(x => x.Map<PostDetailResponse>(post)).Returns(expectedResponse);
            _mockPostReactRepo.Setup(x => x.GetReactCountByPostId(postId)).ReturnsAsync(5);
            _mockCommentRepo.Setup(x => x.CountCommentsByPostId(postId)).ReturnsAsync(10);
            _mockPostReactRepo.Setup(x => x.IsCurrentUserReactedOnPostAsync(postId, currentId)).ReturnsAsync(true);

            // Act
            var result = await _postService.GetPostById(postId, currentId);

            // Assert
            result.Should().NotBeNull();
            result!.PostId.Should().Be(postId);
            result.TotalReacts.Should().Be(5);
            result.TotalComments.Should().Be(10);
            result.IsReactedByCurrentUser.Should().BeTrue();
        }

        [Fact]
        public async Task GetPostById_WhenPostDoesNotExist_ThrowsNotFoundException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _mockPostRepo.Setup(x => x.GetPostById(postId)).ReturnsAsync((Post?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _postService.GetPostById(postId, null));
        }

        #endregion

        #region GetPostDetailByPostId Tests

        [Fact]
        public async Task GetPostDetailByPostId_WhenPostExists_ReturnsPostDetailModel()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var expectedModel = new PostDetailModel
            {
                PostId = postId,
                Content = "Test post content"
            };

            _mockPostRepo.Setup(x => x.GetPostDetailByPostId(postId, currentId)).ReturnsAsync(expectedModel);

            // Act
            var result = await _postService.GetPostDetailByPostId(postId, currentId);

            // Assert
            result.Should().NotBeNull();
            result.PostId.Should().Be(postId);
        }

        [Fact]
        public async Task GetPostDetailByPostId_WhenPostDoesNotExist_ThrowsNotFoundException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            _mockPostRepo.Setup(x => x.GetPostDetailByPostId(postId, currentId)).ReturnsAsync((PostDetailModel?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _postService.GetPostDetailByPostId(postId, currentId));
        }

        #endregion

        #region GetPostDetailByPostCode Tests

        [Fact]
        public async Task GetPostDetailByPostCode_WhenPostExists_ReturnsPostDetailModel()
        {
            // Arrange
            var postCode = "ABC123XYZ";
            var currentId = Guid.NewGuid();
            var expectedModel = new PostDetailModel
            {
                PostCode = postCode,
                Content = "Test post content"
            };

            _mockPostRepo.Setup(x => x.GetPostDetailByPostCode(postCode, currentId)).ReturnsAsync(expectedModel);

            // Act
            var result = await _postService.GetPostDetailByPostCode(postCode, currentId);

            // Assert
            result.Should().NotBeNull();
            result.PostCode.Should().Be(postCode);
        }

        [Fact]
        public async Task GetPostDetailByPostCode_WhenPostDoesNotExist_ThrowsNotFoundException()
        {
            // Arrange
            var postCode = "INVALID";
            var currentId = Guid.NewGuid();
            _mockPostRepo.Setup(x => x.GetPostDetailByPostCode(postCode, currentId)).ReturnsAsync((PostDetailModel?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _postService.GetPostDetailByPostCode(postCode, currentId));
        }

        #endregion

        #region SoftDeletePost Tests

        [Fact]
        public async Task SoftDeletePost_WhenOwner_DeletesSuccessfully()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = currentId
            };

            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _mockPostRepo.Setup(x => x.SoftDeletePostAsync(postId)).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockRealtimeService.Setup(x => x.NotifyPostDeletedAsync(postId, currentId)).Returns(Task.CompletedTask);

            // Act
            var result = await _postService.SoftDeletePost(postId, currentId, false);

            // Assert
            result.Should().Be(currentId);
            _mockPostRepo.Verify(x => x.SoftDeletePostAsync(postId), Times.Once);
        }

        [Fact]
        public async Task SoftDeletePost_WhenAdmin_DeletesSuccessfully()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = ownerId
            };

            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);
            _mockPostRepo.Setup(x => x.SoftDeletePostAsync(postId)).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockRealtimeService.Setup(x => x.NotifyPostDeletedAsync(postId, ownerId)).Returns(Task.CompletedTask);

            // Act
            var result = await _postService.SoftDeletePost(postId, adminId, true);

            // Assert
            result.Should().Be(ownerId);
        }

        [Fact]
        public async Task SoftDeletePost_WhenNotOwnerAndNotAdmin_ThrowsForbiddenException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = ownerId
            };

            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync(post);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() => _postService.SoftDeletePost(postId, otherId, false));
        }

        [Fact]
        public async Task SoftDeletePost_WhenPostDoesNotExist_ThrowsNotFoundException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _mockPostRepo.Setup(x => x.GetPostBasicInfoById(postId)).ReturnsAsync((Post?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _postService.SoftDeletePost(postId, Guid.NewGuid(), false));
        }

        #endregion

        #region GetPostsByAccountId Tests

        [Fact]
        public async Task GetPostsByAccountId_WhenAccountExists_ReturnsPagedResponse()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var posts = new List<PostPersonalListModel>
            {
                new PostPersonalListModel { PostId = Guid.NewGuid() },
                new PostPersonalListModel { PostId = Guid.NewGuid() }
            };

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(accountId)).ReturnsAsync(true);
            _mockPostRepo.Setup(x => x.GetPostsByAccountId(accountId, currentId, 1, 10))
                .ReturnsAsync((posts, 2));

            // Act
            var result = await _postService.GetPostsByAccountId(accountId, currentId, 1, 10);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(2);
            result.TotalItems.Should().Be(2);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(10);
        }

        [Fact]
        public async Task GetPostsByAccountId_WhenAccountDoesNotExist_ThrowsNotFoundException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(accountId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _postService.GetPostsByAccountId(accountId, null, 1, 10));
        }

        #endregion

        #region GetFeedByScoreAsync Tests

        [Fact]
        public async Task GetFeedByScoreAsync_WithValidLimit_ReturnsFeed()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var feed = new List<PostFeedModel>
            {
                new PostFeedModel { PostId = Guid.NewGuid() },
                new PostFeedModel { PostId = Guid.NewGuid() }
            };

            _mockPostRepo.Setup(x => x.GetFeedByScoreAsync(currentId, null, null, 10)).ReturnsAsync(feed);

            // Act
            var result = await _postService.GetFeedByScoreAsync(currentId, null, null, 10);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetFeedByScoreAsync_WithNegativeLimit_DefaultsTo10()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var feed = new List<PostFeedModel>();

            _mockPostRepo.Setup(x => x.GetFeedByScoreAsync(currentId, null, null, 10)).ReturnsAsync(feed);

            // Act
            var result = await _postService.GetFeedByScoreAsync(currentId, null, null, -5);

            // Assert
            _mockPostRepo.Verify(x => x.GetFeedByScoreAsync(currentId, null, null, 10), Times.Once);
        }

        [Fact]
        public async Task GetFeedByScoreAsync_WithLimitOver50_CapsAt50()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var feed = new List<PostFeedModel>();

            _mockPostRepo.Setup(x => x.GetFeedByScoreAsync(currentId, null, null, 50)).ReturnsAsync(feed);

            // Act
            var result = await _postService.GetFeedByScoreAsync(currentId, null, null, 100);

            // Assert
            _mockPostRepo.Verify(x => x.GetFeedByScoreAsync(currentId, null, null, 50), Times.Once);
        }

        #endregion

        #region UpdatePostContent Tests

        [Fact]
        public async Task UpdatePostContent_WhenValidRequest_UpdatesSuccessfully()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = currentId,
                Content = "Old content",
                Privacy = PostPrivacyEnum.Public,
                Medias = new List<PostMedia> { new PostMedia() }
            };

            var request = new PostUpdateContentRequest
            {
                Content = "New content",
                Privacy = (int)PostPrivacyEnum.FollowOnly
            };

            _mockPostRepo.Setup(x => x.GetPostForUpdateContent(postId)).ReturnsAsync(post);
            _mockPostRepo.Setup(x => x.UpdatePost(It.IsAny<Post>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockRealtimeService.Setup(x => x.NotifyPostContentUpdatedAsync(postId, currentId, It.IsAny<PostUpdateContentResponse>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _postService.UpdatePostContent(postId, currentId, request);

            // Assert
            result.Should().NotBeNull();
            result.PostId.Should().Be(postId);
            result.Content.Should().Be("New content");
            result.Privacy.Should().Be(PostPrivacyEnum.FollowOnly);
        }

        [Fact]
        public async Task UpdatePostContent_WhenNotOwner_ThrowsForbiddenException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = ownerId
            };

            _mockPostRepo.Setup(x => x.GetPostForUpdateContent(postId)).ReturnsAsync(post);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _postService.UpdatePostContent(postId, otherId, new PostUpdateContentRequest()));
        }

        [Fact]
        public async Task UpdatePostContent_WhenPostDoesNotExist_ThrowsNotFoundException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _mockPostRepo.Setup(x => x.GetPostForUpdateContent(postId)).ReturnsAsync((Post?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _postService.UpdatePostContent(postId, Guid.NewGuid(), new PostUpdateContentRequest()));
        }

        [Fact]
        public async Task UpdatePostContent_WhenInvalidPrivacy_ThrowsBadRequestException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = currentId
            };

            var request = new PostUpdateContentRequest
            {
                Privacy = 999 // Invalid
            };

            _mockPostRepo.Setup(x => x.GetPostForUpdateContent(postId)).ReturnsAsync(post);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _postService.UpdatePostContent(postId, currentId, request));
        }

        #endregion
    }
}
