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
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Tests.Services
{
    public class StoryServiceTests
    {
        private readonly Mock<IStoryRepository> _storyRepositoryMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<ICloudinaryService> _cloudinaryServiceMock;
        private readonly Mock<IFileTypeDetector> _fileTypeDetectorMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly StoryService _storyService;

        public StoryServiceTests()
        {
            _storyRepositoryMock = new Mock<IStoryRepository>();
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
                BackgroundColorKey = "bg-midnight",
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
                .WithMessage("Text style keys are only allowed for text story.");
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
        public async Task SoftDeleteStoryAsync_WhenCurrentUserIsOwner_SoftDeletesAndCommits()
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
            _unitOfWorkMock
                .Setup(x => x.CommitAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _storyService.SoftDeleteStoryAsync(storyId, currentId);

            // Assert
            story.IsDeleted.Should().BeTrue();
            _storyRepositoryMock.Verify(x => x.UpdateStoryAsync(story), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }
    }
}
