using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using CloudM.Application.DTOs.PostDTOs;
using CloudM.Application.Helpers.FileTypeHelpers;
using CloudM.Infrastructure.Services.Cloudinary;
using CloudM.Application.Services.PostServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Application.Services.StoryViewServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Infrastructure.Repositories.Comments;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.PostMedias;
using CloudM.Infrastructure.Repositories.PostReacts;
using CloudM.Infrastructure.Repositories.PostSaves;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using Xunit;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class PostServiceTests
    {
        private readonly Mock<IPostRepository> _mockPostRepo;
        private readonly Mock<IPostMediaRepository> _mockPostMediaRepo;
        private readonly Mock<IPostReactRepository> _mockPostReactRepo;
        private readonly Mock<IPostSaveRepository> _mockPostSaveRepo;
        private readonly Mock<ICommentRepository> _mockCommentRepo;
        private readonly Mock<IAccountRepository> _mockAccountRepo;
        private readonly Mock<IFollowRepository> _mockFollowRepo;
        private readonly Mock<ICloudinaryService> _mockCloudinaryService;
        private readonly Mock<IFileTypeDetector> _mockFileTypeDetector;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IRealtimeService> _mockRealtimeService;
        private readonly Mock<IStoryViewService> _mockStoryViewService;
        private readonly Mock<IAccountBlockRepository> _mockAccountBlockRepo;
        private readonly PostService _postService;

        public PostServiceTests()
        {
            _mockPostRepo = new Mock<IPostRepository>();
            _mockPostMediaRepo = new Mock<IPostMediaRepository>();
            _mockPostReactRepo = new Mock<IPostReactRepository>();
            _mockPostSaveRepo = new Mock<IPostSaveRepository>();
            _mockCommentRepo = new Mock<ICommentRepository>();
            _mockAccountRepo = new Mock<IAccountRepository>();
            _mockFollowRepo = new Mock<IFollowRepository>();
            _mockCloudinaryService = new Mock<ICloudinaryService>();
            _mockFileTypeDetector = new Mock<IFileTypeDetector>();
            _mockMapper = new Mock<IMapper>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRealtimeService = new Mock<IRealtimeService>();
            _mockStoryViewService = new Mock<IStoryViewService>();
            _mockAccountBlockRepo = new Mock<IAccountBlockRepository>();

            _mockAccountBlockRepo
                .Setup(x => x.GetRelationsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<AccountBlockRelationModel>());

            _postService = new PostService(
                _mockPostReactRepo.Object,
                _mockPostSaveRepo.Object,
                _mockPostMediaRepo.Object,
                _mockPostRepo.Object,
                _mockCommentRepo.Object,
                _mockAccountRepo.Object,
                _mockFollowRepo.Object,
                _mockCloudinaryService.Object,
                _mockFileTypeDetector.Object,
                _mockMapper.Object,
                _mockUnitOfWork.Object,
                _mockRealtimeService.Object,
                _mockStoryViewService.Object,
                null,
                _mockAccountBlockRepo.Object
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
            _mockCommentRepo.Setup(x => x.CountCommentsByPostId(postId, currentId)).ReturnsAsync(10);
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

        #region GetPostsByAccountIdByCursorAsync Tests

        [Fact]
        public async Task GetPostsByAccountIdByCursorAsync_WhenAccountExists_ReturnsCursorResponse()
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
            _mockPostRepo.Setup(x => x.GetPostsByAccountIdByCursor(accountId, currentId, null, null, 11))
                .ReturnsAsync(posts);

            // Act
            var result = await _postService.GetPostsByAccountIdByCursorAsync(accountId, currentId, null, null, 10);

            // Assert
            result.Items.Should().HaveCount(2);
            result.HasMore.Should().BeFalse();
        }

        [Fact]
        public async Task GetPostsByAccountIdByCursorAsync_WhenAccountDoesNotExist_ThrowsNotFoundException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(accountId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _postService.GetPostsByAccountIdByCursorAsync(accountId, null, null, null, 10));
        }

        #endregion

        #region GetFeedByScoreAsync Tests

        [Fact]
        public async Task GetFeedByScoreAsync_WithValidLimit_ReturnsFeed()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var authorA = Guid.NewGuid();
            var authorB = Guid.NewGuid();
            var feed = new List<PostFeedModel>
            {
                new PostFeedModel
                {
                    PostId = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    Author = new AccountOnFeedModel
                    {
                        AccountId = authorA,
                        Username = "author.a",
                        FullName = "Author A",
                        Status = AccountStatusEnum.Active
                    },
                    Medias = new List<MediaPostPersonalListModel>
                    {
                        new MediaPostPersonalListModel
                        {
                            MediaId = Guid.NewGuid(),
                            MediaUrl = "https://cdn.example.com/a.jpg",
                            Type = MediaTypeEnum.Image
                        }
                    }
                },
                new PostFeedModel
                {
                    PostId = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    Author = new AccountOnFeedModel
                    {
                        AccountId = authorB,
                        Username = "author.b",
                        FullName = "Author B",
                        Status = AccountStatusEnum.Active
                    },
                    Medias = new List<MediaPostPersonalListModel>
                    {
                        new MediaPostPersonalListModel
                        {
                            MediaId = Guid.NewGuid(),
                            MediaUrl = "https://cdn.example.com/b.jpg",
                            Type = MediaTypeEnum.Image
                        }
                    }
                }
            };

            _mockPostRepo.Setup(x => x.GetFeedByScoreAsync(currentId, null, null, 10)).ReturnsAsync(feed);
            _mockStoryViewService.Setup(x => x.GetStoryRingStatesForAuthorsAsync(currentId, It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, StoryRingStateEnum>
                {
                    { authorA, StoryRingStateEnum.Unseen },
                    { authorB, StoryRingStateEnum.Seen }
                });

            // Act
            var result = await _postService.GetFeedByScoreAsync(currentId, null, null, 10);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(x => x.Author.AccountId == authorA && x.Author.StoryRingState == StoryRingStateEnum.Unseen);
            result.Should().Contain(x => x.Author.AccountId == authorB && x.Author.StoryRingState == StoryRingStateEnum.Seen);
            result.Should().OnlyContain(x => x.Medias != null && x.Medias.Count == 1);
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
            _mockStoryViewService.Verify(x => x.GetStoryRingStatesForAuthorsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
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
            _mockStoryViewService.Verify(x => x.GetStoryRingStatesForAuthorsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
        }

        #endregion

        #region CreatePost Tag Visibility Tests

        [Fact]
        public async Task CreatePost_WhenPrivatePrivacyWithTaggedAccounts_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var taggedId = Guid.NewGuid();
            var currentAccount = new Account
            {
                AccountId = currentId,
                Status = AccountStatusEnum.Active
            };

            var taggedAccount = new Account
            {
                AccountId = taggedId,
                Status = AccountStatusEnum.Active,
                Settings = new AccountSettings
                {
                    AccountId = taggedId,
                    TagPermission = TagPermissionEnum.Anyone
                }
            };

            var request = new PostCreateRequest
            {
                Privacy = (int)PostPrivacyEnum.Private,
                TaggedAccountIds = new List<Guid> { taggedId },
                MediaFiles = new List<IFormFile>()
            };

            _mockAccountRepo.Setup(x => x.GetAccountById(currentId)).ReturnsAsync(currentAccount);
            _mockAccountRepo.Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { taggedAccount });

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _postService.CreatePost(currentId, request));
            _mockPostRepo.Verify(x => x.AddPost(It.IsAny<Post>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task CreatePost_WhenFollowOnlyPrivacyWithNonFollowerTag_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var taggedId = Guid.NewGuid();
            var currentAccount = new Account
            {
                AccountId = currentId,
                Status = AccountStatusEnum.Active
            };

            var taggedAccount = new Account
            {
                AccountId = taggedId,
                Status = AccountStatusEnum.Active,
                Settings = new AccountSettings
                {
                    AccountId = taggedId,
                    TagPermission = TagPermissionEnum.Anyone
                }
            };

            var request = new PostCreateRequest
            {
                Privacy = (int)PostPrivacyEnum.FollowOnly,
                TaggedAccountIds = new List<Guid> { taggedId },
                MediaFiles = new List<IFormFile>()
            };

            _mockAccountRepo.Setup(x => x.GetAccountById(currentId)).ReturnsAsync(currentAccount);
            _mockAccountRepo.Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { taggedAccount });
            _mockFollowRepo.Setup(x => x.GetFollowerIdsInTargetsAsync(currentId, It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new HashSet<Guid>());

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _postService.CreatePost(currentId, request));
            _mockPostRepo.Verify(x => x.AddPost(It.IsAny<Post>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task CreatePost_WhenTaggedAccountIsBlocked_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var taggedId = Guid.NewGuid();
            var currentAccount = new Account
            {
                AccountId = currentId,
                Status = AccountStatusEnum.Active
            };

            var taggedAccount = new Account
            {
                AccountId = taggedId,
                Status = AccountStatusEnum.Active,
                Settings = new AccountSettings
                {
                    AccountId = taggedId,
                    TagPermission = TagPermissionEnum.Anyone
                }
            };

            var request = new PostCreateRequest
            {
                Privacy = (int)PostPrivacyEnum.Public,
                TaggedAccountIds = new List<Guid> { taggedId },
                MediaFiles = new List<IFormFile>()
            };

            _mockAccountRepo.Setup(x => x.GetAccountById(currentId)).ReturnsAsync(currentAccount);
            _mockAccountRepo.Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { taggedAccount });
            _mockAccountBlockRepo
                .Setup(x => x.GetRelationsAsync(
                    currentId,
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<AccountBlockRelationModel>
                {
                    new()
                    {
                        TargetId = taggedId,
                        IsBlockedByCurrentUser = true
                    }
                });

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _postService.CreatePost(currentId, request));
            _mockPostRepo.Verify(x => x.AddPost(It.IsAny<Post>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Never);
        }

        #endregion

        #region UpdatePost Tests

        [Fact]
        public async Task UpdatePost_WhenRequestContainsRemoveMediaIds_ThrowsBadRequestException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = currentId,
                Account = new Account { AccountId = currentId, Status = AccountStatusEnum.Active },
                Medias = new List<PostMedia> { new PostMedia { MediaId = Guid.NewGuid(), PostId = postId } }
            };

            var request = new PostUpdateRequest
            {
                RemoveMediaIds = new List<Guid> { Guid.NewGuid() }
            };

            _mockPostRepo.Setup(x => x.GetPostById(postId)).ReturnsAsync(post);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _postService.UpdatePost(postId, currentId, request));
            _mockPostRepo.Verify(x => x.UpdatePost(It.IsAny<Post>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdatePost_WhenRequestContainsNewMediaFiles_ThrowsBadRequestException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = currentId,
                Account = new Account { AccountId = currentId, Status = AccountStatusEnum.Active },
                Medias = new List<PostMedia> { new PostMedia { MediaId = Guid.NewGuid(), PostId = postId } }
            };

            var request = new PostUpdateRequest
            {
                NewMediaFiles = new List<IFormFile> { new Mock<IFormFile>().Object }
            };

            _mockPostRepo.Setup(x => x.GetPostById(postId)).ReturnsAsync(post);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _postService.UpdatePost(postId, currentId, request));
            _mockPostRepo.Verify(x => x.UpdatePost(It.IsAny<Post>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Never);
        }

        #endregion

        #region UpdatePostContent Tests

        [Fact]
        public async Task UpdatePostContent_WhenPrivacyChangesToPrivate_WithExistingTags_UpdatesSuccessfully()
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
                Privacy = (int)PostPrivacyEnum.Private
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
            result.Privacy.Should().Be(PostPrivacyEnum.Private);
            _mockPostRepo.Verify(x => x.GetTaggedAccountIdsByPostIdAsync(It.IsAny<Guid>()), Times.Never);
            _mockPostRepo.Verify(x => x.UpdatePost(It.IsAny<Post>()), Times.Once);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdatePostContent_WhenPrivacyChangesToFollowOnly_WithNonFollowerTaggedAccount_UpdatesSuccessfully()
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
            result.Privacy.Should().Be(PostPrivacyEnum.FollowOnly);
            _mockPostRepo.Verify(x => x.GetTaggedAccountIdsByPostIdAsync(It.IsAny<Guid>()), Times.Never);
            _mockFollowRepo.Verify(x => x.GetFollowerIdsInTargetsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            _mockPostRepo.Verify(x => x.UpdatePost(It.IsAny<Post>()), Times.Once);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Once);
        }

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
        public async Task UpdatePostContent_WhenNoTagMutation_DoesNotQueryTagLogic()
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
                Content = "New content only",
                Privacy = (int)PostPrivacyEnum.Public
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
            _mockPostRepo.Verify(x => x.GetTaggedAccountIdsByPostIdAsync(It.IsAny<Guid>()), Times.Never);
            _mockAccountRepo.Verify(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()), Times.Never);
            _mockFollowRepo.Verify(x => x.GetFollowingIdsAsync(It.IsAny<Guid>()), Times.Never);
            _mockPostRepo.Verify(x => x.AddPostTagsAsync(It.IsAny<IEnumerable<PostTag>>()), Times.Never);
            _mockPostRepo.Verify(x => x.RemovePostTagsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
        }

        [Fact]
        public async Task UpdatePostContent_WhenAddAndRemoveContainSameAccount_ThrowsBadRequestException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var tagId = Guid.NewGuid();
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
                AddNewTagIds = new List<Guid> { tagId },
                RemoveTagIds = new List<Guid> { tagId }
            };

            _mockPostRepo.Setup(x => x.GetPostForUpdateContent(postId)).ReturnsAsync(post);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _postService.UpdatePostContent(postId, currentId, request));
        }

        [Fact]
        public async Task UpdatePostContent_WhenAddNewTagAnyonePermission_AddsTagSuccessfully()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
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
                AddNewTagIds = new List<Guid> { targetId }
            };

            var targetAccount = new Account
            {
                AccountId = targetId,
                Username = "target.user",
                FullName = "Target User",
                Status = AccountStatusEnum.Active,
                Settings = new AccountSettings
                {
                    AccountId = targetId,
                    TagPermission = TagPermissionEnum.Anyone
                }
            };

            _mockPostRepo.Setup(x => x.GetPostForUpdateContent(postId)).ReturnsAsync(post);
            _mockPostRepo.Setup(x => x.GetTaggedAccountIdsByPostIdAsync(postId)).ReturnsAsync(new List<Guid>());
            _mockAccountRepo.Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { targetAccount });
            _mockPostRepo.Setup(x => x.AddPostTagsAsync(It.IsAny<IEnumerable<PostTag>>())).Returns(Task.CompletedTask);
            _mockPostRepo.Setup(x => x.UpdatePost(It.IsAny<Post>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockRealtimeService.Setup(x => x.NotifyPostContentUpdatedAsync(postId, currentId, It.IsAny<PostUpdateContentResponse>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _postService.UpdatePostContent(postId, currentId, request);

            // Assert
            result.Should().NotBeNull();
            _mockPostRepo.Verify(
                x => x.AddPostTagsAsync(It.Is<IEnumerable<PostTag>>(tags =>
                    tags.Any(t => t.PostId == postId && t.TaggedAccountId == targetId))),
                Times.Once);
            _mockFollowRepo.Verify(x => x.GetFollowingIdsAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task UpdatePostContent_WhenAddTagInFollowOnly_WithNonFollowerTaggedAccount_ThrowsBadRequestException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = currentId,
                Content = "Old content",
                Privacy = PostPrivacyEnum.FollowOnly,
                Medias = new List<PostMedia> { new PostMedia() }
            };

            var request = new PostUpdateContentRequest
            {
                AddNewTagIds = new List<Guid> { targetId }
            };

            var targetAccount = new Account
            {
                AccountId = targetId,
                Username = "target.user",
                FullName = "Target User",
                Status = AccountStatusEnum.Active,
                Settings = new AccountSettings
                {
                    AccountId = targetId,
                    TagPermission = TagPermissionEnum.Anyone
                }
            };

            _mockPostRepo.Setup(x => x.GetPostForUpdateContent(postId)).ReturnsAsync(post);
            _mockPostRepo.Setup(x => x.GetTaggedAccountIdsByPostIdAsync(postId)).ReturnsAsync(new List<Guid>());
            _mockAccountRepo.Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { targetAccount });
            _mockFollowRepo.Setup(x => x.GetFollowerIdsInTargetsAsync(currentId, It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new HashSet<Guid>());

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _postService.UpdatePostContent(postId, currentId, request));
            _mockPostRepo.Verify(x => x.AddPostTagsAsync(It.IsAny<IEnumerable<PostTag>>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdatePostContent_WhenOnlyTagListChanges_DoesNotUpdatePostUpdatedAt()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = currentId,
                Content = "Stable content",
                Privacy = PostPrivacyEnum.Public,
                UpdatedAt = null,
                Medias = new List<PostMedia> { new PostMedia() }
            };

            var request = new PostUpdateContentRequest
            {
                AddNewTagIds = new List<Guid> { targetId }
            };

            var targetAccount = new Account
            {
                AccountId = targetId,
                Username = "target.user",
                FullName = "Target User",
                Status = AccountStatusEnum.Active,
                Settings = new AccountSettings
                {
                    AccountId = targetId,
                    TagPermission = TagPermissionEnum.Anyone
                }
            };

            _mockPostRepo.Setup(x => x.GetPostForUpdateContent(postId)).ReturnsAsync(post);
            _mockPostRepo.Setup(x => x.GetTaggedAccountIdsByPostIdAsync(postId)).ReturnsAsync(new List<Guid>());
            _mockAccountRepo.Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { targetAccount });
            _mockPostRepo.Setup(x => x.AddPostTagsAsync(It.IsAny<IEnumerable<PostTag>>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockRealtimeService.Setup(x => x.NotifyPostContentUpdatedAsync(postId, currentId, It.IsAny<PostUpdateContentResponse>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _postService.UpdatePostContent(postId, currentId, request);

            // Assert
            result.Should().NotBeNull();
            result.UpdatedAt.Should().BeNull();
            _mockPostRepo.Verify(x => x.UpdatePost(It.IsAny<Post>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdatePostContent_WhenOnlyPrivacyChanges_DoesNotUpdateUpdatedAt()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var previousUpdatedAt = DateTime.UtcNow.AddHours(-2);
            var post = new Post
            {
                PostId = postId,
                AccountId = currentId,
                Content = "Stable content",
                Privacy = PostPrivacyEnum.Public,
                UpdatedAt = previousUpdatedAt,
                Medias = new List<PostMedia> { new PostMedia() }
            };

            var request = new PostUpdateContentRequest
            {
                Privacy = (int)PostPrivacyEnum.Private
            };

            _mockPostRepo.Setup(x => x.GetPostForUpdateContent(postId)).ReturnsAsync(post);
            _mockPostRepo.Setup(x => x.GetTaggedAccountIdsByPostIdAsync(postId)).ReturnsAsync(new List<Guid>());
            _mockPostRepo.Setup(x => x.UpdatePost(It.IsAny<Post>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockRealtimeService.Setup(x => x.NotifyPostContentUpdatedAsync(postId, currentId, It.IsAny<PostUpdateContentResponse>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _postService.UpdatePostContent(postId, currentId, request);

            // Assert
            result.Should().NotBeNull();
            result.Privacy.Should().Be(PostPrivacyEnum.Private);
            result.UpdatedAt.Should().Be(previousUpdatedAt);
            _mockPostRepo.Verify(x => x.UpdatePost(It.IsAny<Post>()), Times.Once);
            _mockPostRepo.Verify(x => x.GetTaggedAccountIdsByPostIdAsync(postId), Times.Never);
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

        [Fact]
        public async Task UpdatePostContent_WhenPostHasNoMedia_ThrowsBadRequestException()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var post = new Post
            {
                PostId = postId,
                AccountId = currentId,
                Content = "still has text",
                Medias = new List<PostMedia>()
            };

            _mockPostRepo.Setup(x => x.GetPostForUpdateContent(postId)).ReturnsAsync(post);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _postService.UpdatePostContent(postId, currentId, new PostUpdateContentRequest()));
            _mockPostRepo.Verify(x => x.UpdatePost(It.IsAny<Post>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Never);
        }

        #endregion
    }
}
