using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using AutoMapper;
using SocialNetwork.Application.DTOs.StoryDTOs;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Application.Services.StoryServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.Stories;
using SocialNetwork.Infrastructure.Repositories.StoryHighlights;
using SocialNetwork.Infrastructure.Repositories.StoryViews;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using SocialNetwork.Infrastructure.Models;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Tests.Services
{
    public class StoryServiceTests
    {
        private readonly Mock<IStoryRepository> _storyRepositoryMock;
        private readonly Mock<IStoryHighlightRepository> _storyHighlightRepositoryMock;
        private readonly Mock<IStoryViewRepository> _storyViewRepositoryMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<ICloudinaryService> _cloudinaryServiceMock;
        private readonly Mock<IFileTypeDetector> _fileTypeDetectorMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly StoryService _storyService;

        public StoryServiceTests()
        {
            _storyRepositoryMock = new Mock<IStoryRepository>();
            _storyHighlightRepositoryMock = new Mock<IStoryHighlightRepository>();
            _storyViewRepositoryMock = new Mock<IStoryViewRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _cloudinaryServiceMock = new Mock<ICloudinaryService>();
            _fileTypeDetectorMock = new Mock<IFileTypeDetector>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _mapperMock = new Mock<IMapper>();

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<StoryDetailResponse>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<StoryDetailResponse>> operation, Func<Task>? _) => operation());
            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<bool>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<bool>> operation, Func<Task>? _) => operation());
            _unitOfWorkMock
                .Setup(x => x.CommitAsync())
                .Returns(Task.CompletedTask);
            _mapperMock
                .Setup(x => x.Map<StoryDetailResponse>(It.IsAny<Story>()))
                .Returns<Story>(story => new StoryDetailResponse
                {
                    StoryId = story.StoryId,
                    AccountId = story.AccountId,
                    ContentType = (int)story.ContentType,
                    MediaUrl = story.MediaUrl,
                    TextContent = story.TextContent,
                    BackgroundColorKey = story.BackgroundColorKey,
                    FontTextKey = story.FontTextKey,
                    FontSizeKey = story.FontSizeKey,
                    TextColorKey = story.TextColorKey,
                    Privacy = (int)story.Privacy,
                    CreatedAt = story.CreatedAt,
                    ExpiresAt = story.ExpiresAt,
                    IsDeleted = story.IsDeleted
                });

            _storyService = new StoryService(
                _storyRepositoryMock.Object,
                _storyHighlightRepositoryMock.Object,
                _storyViewRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _cloudinaryServiceMock.Object,
                _fileTypeDetectorMock.Object,
                _unitOfWorkMock.Object,
                _mapperMock.Object);
        }

        [Fact]
        public async Task CreateStoryAsync_WhenAccountNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var request = new StoryCreateRequest
            {
                ContentType = (int)StoryContentTypeEnum.Image,
                MediaFile = new Mock<IFormFile>().Object,
                ExpiresEnum = (int)StoryExpiresEnum.Hours24
            };

            _accountRepositoryMock
                .Setup(x => x.GetAccountById(accountId))
                .ReturnsAsync((Account?)null);

            // Act
            var act = () => _storyService.CreateStoryAsync(accountId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage($"Account with ID {accountId} not found.");
        }

        [Fact]
        public async Task CreateStoryAsync_WhenAccountIsNotActive_ThrowsForbiddenException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var request = new StoryCreateRequest
            {
                ContentType = (int)StoryContentTypeEnum.Image,
                MediaFile = new Mock<IFormFile>().Object,
                ExpiresEnum = (int)StoryExpiresEnum.Hours24
            };

            _accountRepositoryMock
                .Setup(x => x.GetAccountById(accountId))
                .ReturnsAsync(new Account
                {
                    AccountId = accountId,
                    Status = AccountStatusEnum.Suspended
                });

            // Act
            var act = () => _storyService.CreateStoryAsync(accountId, request);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You must reactivate your account to create stories.");
        }

        [Fact]
        public async Task CreateStoryAsync_WithImageStory_UsesTransactionAndCommits()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var mediaFileMock = new Mock<IFormFile>();
            mediaFileMock.SetupGet(x => x.Length).Returns(1024);
            var mediaFile = mediaFileMock.Object;
            var request = new StoryCreateRequest
            {
                ContentType = (int)StoryContentTypeEnum.Image,
                MediaFile = mediaFile,
                TextContent = null,
                Privacy = (int)StoryPrivacyEnum.FollowOnly,
                ExpiresEnum = (int)StoryExpiresEnum.Hours6
            };

            _accountRepositoryMock
                .Setup(x => x.GetAccountById(accountId))
                .ReturnsAsync(new Account
                {
                    AccountId = accountId,
                    Status = AccountStatusEnum.Active
                });
            _fileTypeDetectorMock
                .Setup(x => x.GetMediaTypeAsync(mediaFile))
                .ReturnsAsync(MediaTypeEnum.Image);
            _cloudinaryServiceMock
                .Setup(x => x.UploadImageAsync(mediaFile))
                .ReturnsAsync("https://cdn.example.com/image.jpg");

            Story? capturedStory = null;
            _storyRepositoryMock
                .Setup(x => x.AddStoryAsync(It.IsAny<Story>()))
                .Callback<Story>(story => capturedStory = story)
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.CommitAsync())
                .Returns(Task.CompletedTask);

            // Act
            var result = await _storyService.CreateStoryAsync(accountId, request);

            // Assert
            capturedStory.Should().NotBeNull();
            capturedStory!.AccountId.Should().Be(accountId);
            capturedStory.ContentType.Should().Be(StoryContentTypeEnum.Image);
            capturedStory.MediaUrl.Should().Be("https://cdn.example.com/image.jpg");
            capturedStory.TextContent.Should().BeNull();
            capturedStory.BackgroundColorKey.Should().BeNull();
            capturedStory.FontTextKey.Should().BeNull();
            capturedStory.FontSizeKey.Should().BeNull();
            capturedStory.TextColorKey.Should().BeNull();
            capturedStory.Privacy.Should().Be(StoryPrivacyEnum.FollowOnly);
            capturedStory.ExpiresAt.Should().BeCloseTo(capturedStory.CreatedAt.AddHours(6), TimeSpan.FromSeconds(1));

            result.StoryId.Should().Be(capturedStory.StoryId);
            result.AccountId.Should().Be(accountId);
            result.ContentType.Should().Be((int)StoryContentTypeEnum.Image);
            result.Privacy.Should().Be((int)StoryPrivacyEnum.FollowOnly);

            _unitOfWorkMock.Verify(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<StoryDetailResponse>>>(),
                It.IsAny<Func<Task>?>()), Times.Once);
            _fileTypeDetectorMock.Verify(x => x.GetMediaTypeAsync(mediaFile), Times.Once);
            _cloudinaryServiceMock.Verify(x => x.UploadImageAsync(mediaFile), Times.Once);
            _storyRepositoryMock.Verify(x => x.AddStoryAsync(It.IsAny<Story>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task CreateStoryAsync_WithInvalidExpiresEnum_DefaultsTo24Hours()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var mediaFileMock = new Mock<IFormFile>();
            mediaFileMock.SetupGet(x => x.Length).Returns(1024);
            var mediaFile = mediaFileMock.Object;
            var request = new StoryCreateRequest
            {
                ContentType = (int)StoryContentTypeEnum.Video,
                MediaFile = mediaFile,
                ExpiresEnum = 999 // invalid
            };

            _accountRepositoryMock
                .Setup(x => x.GetAccountById(accountId))
                .ReturnsAsync(new Account
                {
                    AccountId = accountId,
                    Status = AccountStatusEnum.Active
                });
            _fileTypeDetectorMock
                .Setup(x => x.GetMediaTypeAsync(mediaFile))
                .ReturnsAsync(MediaTypeEnum.Video);
            _cloudinaryServiceMock
                .Setup(x => x.UploadVideoAsync(mediaFile))
                .ReturnsAsync("https://cdn.example.com/video.mp4");

            Story? capturedStory = null;
            _storyRepositoryMock
                .Setup(x => x.AddStoryAsync(It.IsAny<Story>()))
                .Callback<Story>(story => capturedStory = story)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _storyService.CreateStoryAsync(accountId, request);

            // Assert
            capturedStory.Should().NotBeNull();
            capturedStory!.ExpiresAt.Should().BeCloseTo(capturedStory.CreatedAt.AddHours(24), TimeSpan.FromSeconds(1));
            result.ExpiresAt.Should().BeCloseTo(result.CreatedAt.AddHours(24), TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task CreateStoryAsync_WithTextStory_ClearsMediaFieldsAndKeepsTrimmedText()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var request = new StoryCreateRequest
            {
                ContentType = (int)StoryContentTypeEnum.Text,
                MediaFile = null,
                TextContent = "  hello story  ",
                BackgroundColorKey = "  bg-midnight  ",
                FontTextKey = "  font-modern  ",
                FontSizeKey = "  size-lg  ",
                TextColorKey = "  text-ivory  ",
                Privacy = null,
                ExpiresEnum = (int)StoryExpiresEnum.Hours12
            };

            _accountRepositoryMock
                .Setup(x => x.GetAccountById(accountId))
                .ReturnsAsync(new Account
                {
                    AccountId = accountId,
                    Status = AccountStatusEnum.Active
                });

            Story? capturedStory = null;
            _storyRepositoryMock
                .Setup(x => x.AddStoryAsync(It.IsAny<Story>()))
                .Callback<Story>(story => capturedStory = story)
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.CommitAsync())
                .Returns(Task.CompletedTask);

            // Act
            var result = await _storyService.CreateStoryAsync(accountId, request);

            // Assert
            capturedStory.Should().NotBeNull();
            capturedStory!.ContentType.Should().Be(StoryContentTypeEnum.Text);
            capturedStory.MediaUrl.Should().BeNull();
            capturedStory.TextContent.Should().Be("hello story");
            capturedStory.BackgroundColorKey.Should().Be("bg-midnight");
            capturedStory.FontTextKey.Should().Be("font-modern");
            capturedStory.FontSizeKey.Should().Be("size-lg");
            capturedStory.TextColorKey.Should().Be("text-ivory");
            capturedStory.Privacy.Should().Be(StoryPrivacyEnum.Public); // default when null
            capturedStory.ExpiresAt.Should().BeCloseTo(capturedStory.CreatedAt.AddHours(12), TimeSpan.FromSeconds(1));

            result.MediaUrl.Should().BeNull();
            result.TextContent.Should().Be("hello story");
            result.BackgroundColorKey.Should().Be("bg-midnight");
            result.FontTextKey.Should().Be("font-modern");
            result.FontSizeKey.Should().Be("size-lg");
            result.TextColorKey.Should().Be("text-ivory");
            result.Privacy.Should().Be((int)StoryPrivacyEnum.Public);
            _fileTypeDetectorMock.Verify(x => x.GetMediaTypeAsync(It.IsAny<IFormFile>()), Times.Never);
            _cloudinaryServiceMock.Verify(x => x.UploadImageAsync(It.IsAny<IFormFile>()), Times.Never);
            _cloudinaryServiceMock.Verify(x => x.UploadVideoAsync(It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task CreateStoryAsync_WithImageStoryAndTextStyleKeys_ThrowsBadRequestException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var mediaFileMock = new Mock<IFormFile>();
            mediaFileMock.SetupGet(x => x.Length).Returns(1024);
            var request = new StoryCreateRequest
            {
                ContentType = (int)StoryContentTypeEnum.Image,
                MediaFile = mediaFileMock.Object,
                FontTextKey = "font-modern",
                ExpiresEnum = (int)StoryExpiresEnum.Hours24
            };

            _accountRepositoryMock
                .Setup(x => x.GetAccountById(accountId))
                .ReturnsAsync(new Account
                {
                    AccountId = accountId,
                    Status = AccountStatusEnum.Active
                });

            // Act
            var act = () => _storyService.CreateStoryAsync(accountId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Font, size and text color keys are only allowed for text story.");
        }

        [Fact]
        public async Task CreateStoryAsync_WithTextStoryAndMediaFile_ThrowsBadRequestException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var request = new StoryCreateRequest
            {
                ContentType = (int)StoryContentTypeEnum.Text,
                MediaFile = new Mock<IFormFile>().Object,
                TextContent = "hello story",
                ExpiresEnum = (int)StoryExpiresEnum.Hours24
            };

            _accountRepositoryMock
                .Setup(x => x.GetAccountById(accountId))
                .ReturnsAsync(new Account
                {
                    AccountId = accountId,
                    Status = AccountStatusEnum.Active
                });

            // Act
            var act = () => _storyService.CreateStoryAsync(accountId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("MediaFile is not allowed for text story.");
        }

        [Fact]
        public async Task CreateStoryAsync_WithImageStoryAndTextContent_ThrowsBadRequestException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var mediaFileMock = new Mock<IFormFile>();
            mediaFileMock.SetupGet(x => x.Length).Returns(1024);
            var request = new StoryCreateRequest
            {
                ContentType = (int)StoryContentTypeEnum.Image,
                MediaFile = mediaFileMock.Object,
                TextContent = "not allowed",
                ExpiresEnum = (int)StoryExpiresEnum.Hours24
            };

            _accountRepositoryMock
                .Setup(x => x.GetAccountById(accountId))
                .ReturnsAsync(new Account
                {
                    AccountId = accountId,
                    Status = AccountStatusEnum.Active
                });

            // Act
            var act = () => _storyService.CreateStoryAsync(accountId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("TextContent is only allowed for text story.");
        }

        [Fact]
        public async Task UpdateStoryPrivacyAsync_WhenStoryNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var request = new StoryPrivacyUpdateRequest
            {
                Privacy = (int)StoryPrivacyEnum.Private
            };

            _storyRepositoryMock
                .Setup(x => x.GetStoryByIdAsync(storyId))
                .ReturnsAsync((Story?)null);

            // Act
            var act = () => _storyService.UpdateStoryPrivacyAsync(storyId, currentId, request);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage($"Story with ID {storyId} not found.");
        }

        [Fact]
        public async Task UpdateStoryPrivacyAsync_WhenCurrentUserNotOwner_ThrowsForbiddenException()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var request = new StoryPrivacyUpdateRequest
            {
                Privacy = (int)StoryPrivacyEnum.Private
            };

            _storyRepositoryMock
                .Setup(x => x.GetStoryByIdAsync(storyId))
                .ReturnsAsync(new Story
                {
                    StoryId = storyId,
                    AccountId = ownerId,
                    Privacy = StoryPrivacyEnum.Public,
                    ExpiresAt = DateTime.UtcNow.AddHours(2),
                    CreatedAt = DateTime.UtcNow.AddHours(-1)
                });

            // Act
            var act = () => _storyService.UpdateStoryPrivacyAsync(storyId, currentId, request);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You are not authorized to edit this story.");
        }

        [Fact]
        public async Task UpdateStoryPrivacyAsync_WhenValidRequest_UpdatesPrivacyAndCommits()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var story = new Story
            {
                StoryId = storyId,
                AccountId = currentId,
                ContentType = StoryContentTypeEnum.Image,
                MediaUrl = "https://cdn.example.com/story.jpg",
                Privacy = StoryPrivacyEnum.Public,
                ExpiresAt = DateTime.UtcNow.AddHours(2),
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            };
            var request = new StoryPrivacyUpdateRequest
            {
                Privacy = (int)StoryPrivacyEnum.FollowOnly
            };

            _storyRepositoryMock
                .Setup(x => x.GetStoryByIdAsync(storyId))
                .ReturnsAsync(story);
            _storyRepositoryMock
                .Setup(x => x.UpdateStoryAsync(story))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock
                .Setup(x => x.CommitAsync())
                .Returns(Task.CompletedTask);

            // Act
            var result = await _storyService.UpdateStoryPrivacyAsync(storyId, currentId, request);

            // Assert
            story.Privacy.Should().Be(StoryPrivacyEnum.FollowOnly);
            result.Privacy.Should().Be((int)StoryPrivacyEnum.FollowOnly);
            _storyRepositoryMock.Verify(x => x.UpdateStoryAsync(story), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task SoftDeleteStoryAsync_WhenStoryNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var currentId = Guid.NewGuid();

            _storyRepositoryMock
                .Setup(x => x.GetStoryByIdAsync(storyId))
                .ReturnsAsync((Story?)null);

            // Act
            var act = () => _storyService.SoftDeleteStoryAsync(storyId, currentId);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage($"Story with ID {storyId} not found.");
        }

        [Fact]
        public async Task SoftDeleteStoryAsync_WhenCurrentUserNotOwner_ThrowsForbiddenException()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var currentId = Guid.NewGuid();

            _storyRepositoryMock
                .Setup(x => x.GetStoryByIdAsync(storyId))
                .ReturnsAsync(new Story
                {
                    StoryId = storyId,
                    AccountId = ownerId,
                    IsDeleted = false
                });

            // Act
            var act = () => _storyService.SoftDeleteStoryAsync(storyId, currentId);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You are not authorized to delete this story.");
        }

        [Fact]
        public async Task SoftDeleteStoryAsync_WhenCurrentUserIsOwner_SoftDeletesInTransaction()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var story = new Story
            {
                StoryId = storyId,
                AccountId = currentId,
                IsDeleted = false
            };

            _storyRepositoryMock
                .Setup(x => x.GetStoryByIdAsync(storyId))
                .ReturnsAsync(story);
            _storyRepositoryMock
                .Setup(x => x.UpdateStoryAsync(story))
                .Returns(Task.CompletedTask);
            _storyHighlightRepositoryMock
                .Setup(x => x.GetGroupsByOwnerContainingStoryAsync(currentId, storyId))
                .ReturnsAsync(new List<StoryHighlightGroup>());
            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<bool>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<bool>> operation, Func<Task>? _) => operation());

            // Act
            await _storyService.SoftDeleteStoryAsync(storyId, currentId);

            // Assert
            story.IsDeleted.Should().BeTrue();
            _storyRepositoryMock.Verify(x => x.UpdateStoryAsync(story), Times.Once);
            _unitOfWorkMock.Verify(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<Func<Task>?>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task SoftDeleteStoryAsync_WhenStoryIsSingleItemInHighlightGroup_DeletesEmptyGroupAndCover()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var story = new Story
            {
                StoryId = storyId,
                AccountId = currentId,
                IsDeleted = false
            };

            var group = new StoryHighlightGroup
            {
                StoryHighlightGroupId = Guid.NewGuid(),
                AccountId = currentId,
                Name = "Trips",
                CoverImageUrl = "https://res.cloudinary.com/demo/image/upload/v1/group-cover.jpg"
            };

            _storyRepositoryMock
                .Setup(x => x.GetStoryByIdAsync(storyId))
                .ReturnsAsync(story);
            _storyRepositoryMock
                .Setup(x => x.UpdateStoryAsync(story))
                .Returns(Task.CompletedTask);
            _storyHighlightRepositoryMock
                .Setup(x => x.GetGroupsByOwnerContainingStoryAsync(currentId, storyId))
                .ReturnsAsync(new List<StoryHighlightGroup> { group });
            _storyHighlightRepositoryMock
                .Setup(x => x.TryRemoveGroupIfEffectivelyEmptyAsync(group.StoryHighlightGroupId, currentId))
                .ReturnsAsync(true);

            _cloudinaryServiceMock
                .Setup(x => x.GetPublicIdFromUrl(group.CoverImageUrl!))
                .Returns("group-cover");
            _cloudinaryServiceMock
                .Setup(x => x.DeleteMediaAsync("group-cover", MediaTypeEnum.Image))
                .ReturnsAsync(true);

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<bool>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<bool>> operation, Func<Task>? _) => operation());

            // Act
            await _storyService.SoftDeleteStoryAsync(storyId, currentId);

            // Assert
            story.IsDeleted.Should().BeTrue();
            _storyRepositoryMock.Verify(x => x.UpdateStoryAsync(story), Times.Once);
            _storyHighlightRepositoryMock.Verify(
                x => x.TryRemoveGroupIfEffectivelyEmptyAsync(group.StoryHighlightGroupId, currentId),
                Times.Once);
            _cloudinaryServiceMock.Verify(x => x.DeleteMediaAsync("group-cover", MediaTypeEnum.Image), Times.Once);
        }

        [Fact]
        public async Task GetArchivedStoriesAsync_WhenInvalidPaging_NormalizesBeforeQuery()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            _storyRepositoryMock
                .Setup(x => x.GetArchivedStoriesByOwnerAsync(
                    currentId,
                    It.IsAny<DateTime>(),
                    It.IsAny<int>(),
                    It.IsAny<int>()))
                .ReturnsAsync((new List<StoryArchiveItemModel>(), 0));

            // Act
            var result = await _storyService.GetArchivedStoriesAsync(currentId, 0, -5);

            // Assert
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(20);
            _storyRepositoryMock.Verify(x => x.GetArchivedStoriesByOwnerAsync(
                currentId,
                It.IsAny<DateTime>(),
                1,
                20), Times.Once);
        }

        [Fact]
        public async Task GetArchivedStoriesAsync_WhenPageSizeTooLarge_ClampsToMax()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            _storyRepositoryMock
                .Setup(x => x.GetArchivedStoriesByOwnerAsync(
                    currentId,
                    It.IsAny<DateTime>(),
                    It.IsAny<int>(),
                    It.IsAny<int>()))
                .ReturnsAsync((new List<StoryArchiveItemModel>(), 0));

            // Act
            var result = await _storyService.GetArchivedStoriesAsync(currentId, 2, 500);

            // Assert
            result.Page.Should().Be(2);
            result.PageSize.Should().Be(60);
            _storyRepositoryMock.Verify(x => x.GetArchivedStoriesByOwnerAsync(
                currentId,
                It.IsAny<DateTime>(),
                2,
                60), Times.Once);
        }

        [Fact]
        public async Task GetArchivedStoriesAsync_WhenNoItems_ReturnsEmptyAndSkipsViewSummary()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            _storyRepositoryMock
                .Setup(x => x.GetArchivedStoriesByOwnerAsync(
                    currentId,
                    It.IsAny<DateTime>(),
                    1,
                    12))
                .ReturnsAsync((new List<StoryArchiveItemModel>(), 0));

            // Act
            var result = await _storyService.GetArchivedStoriesAsync(currentId, 1, 12);

            // Assert
            result.TotalItems.Should().Be(0);
            result.Items.Should().BeEmpty();
            _storyViewRepositoryMock.Verify(x => x.GetStoryViewSummariesAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetArchivedStoriesAsync_WhenItemsExist_MapsViewCountAndStoryFields()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var storyId1 = Guid.NewGuid();
            var storyId2 = Guid.NewGuid();
            var createdAt1 = DateTime.UtcNow.AddDays(-2);
            var expiresAt1 = createdAt1.AddHours(24);
            var createdAt2 = DateTime.UtcNow.AddDays(-3);
            var expiresAt2 = createdAt2.AddHours(24);

            var archivedItems = new List<StoryArchiveItemModel>
            {
                new StoryArchiveItemModel
                {
                    StoryId = storyId1,
                    ContentType = StoryContentTypeEnum.Text,
                    MediaUrl = null,
                    TextContent = "Archived text",
                    BackgroundColorKey = "accent",
                    FontTextKey = "modern",
                    FontSizeKey = "32",
                    TextColorKey = "light",
                    Privacy = StoryPrivacyEnum.Public,
                    CreatedAt = createdAt1,
                    ExpiresAt = expiresAt1
                },
                new StoryArchiveItemModel
                {
                    StoryId = storyId2,
                    ContentType = StoryContentTypeEnum.Video,
                    MediaUrl = "https://cdn.example.com/archive-video.mp4",
                    TextContent = null,
                    BackgroundColorKey = null,
                    FontTextKey = null,
                    FontSizeKey = null,
                    TextColorKey = null,
                    Privacy = StoryPrivacyEnum.FollowOnly,
                    CreatedAt = createdAt2,
                    ExpiresAt = expiresAt2
                }
            };

            _storyRepositoryMock
                .Setup(x => x.GetArchivedStoriesByOwnerAsync(
                    currentId,
                    It.IsAny<DateTime>(),
                    1,
                    2))
                .ReturnsAsync((archivedItems, 2));

            _storyViewRepositoryMock
                .Setup(x => x.GetStoryViewSummariesAsync(
                    currentId,
                    It.Is<IReadOnlyCollection<Guid>>(ids =>
                        ids.Count == 2 &&
                        ids.Contains(storyId1) &&
                        ids.Contains(storyId2)),
                    3))
                .ReturnsAsync(new Dictionary<Guid, StoryViewSummaryModel>
                {
                    {
                        storyId1,
                        new StoryViewSummaryModel
                        {
                            StoryId = storyId1,
                            TotalViews = 7,
                            TotalReacts = 3
                        }
                    }
                });

            // Act
            var result = await _storyService.GetArchivedStoriesAsync(currentId, 1, 2);
            var items = result.Items.ToList();

            // Assert
            result.TotalItems.Should().Be(2);
            items.Should().HaveCount(2);

            var first = items.Single(x => x.StoryId == storyId1);
            first.ContentType.Should().Be((int)StoryContentTypeEnum.Text);
            first.TextContent.Should().Be("Archived text");
            first.BackgroundColorKey.Should().Be("accent");
            first.FontTextKey.Should().Be("modern");
            first.FontSizeKey.Should().Be("32");
            first.TextColorKey.Should().Be("light");
            first.Privacy.Should().Be((int)StoryPrivacyEnum.Public);
            first.CreatedAt.Should().Be(createdAt1);
            first.ExpiresAt.Should().Be(expiresAt1);
            first.ViewCount.Should().Be(7);
            first.ReactCount.Should().Be(3);

            var second = items.Single(x => x.StoryId == storyId2);
            second.ContentType.Should().Be((int)StoryContentTypeEnum.Video);
            second.MediaUrl.Should().Be("https://cdn.example.com/archive-video.mp4");
            second.Privacy.Should().Be((int)StoryPrivacyEnum.FollowOnly);
            second.CreatedAt.Should().Be(createdAt2);
            second.ExpiresAt.Should().Be(expiresAt2);
            second.ViewCount.Should().Be(0);
            second.ReactCount.Should().Be(0);

            _storyViewRepositoryMock.Verify(x => x.GetStoryViewSummariesAsync(
                currentId,
                It.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.Count == 2 &&
                    ids.Contains(storyId1) &&
                    ids.Contains(storyId2)),
                3), Times.Once);
        }
    }
}
