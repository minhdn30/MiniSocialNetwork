using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
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
        private readonly StoryService _storyService;

        public StoryServiceTests()
        {
            _storyRepositoryMock = new Mock<IStoryRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _cloudinaryServiceMock = new Mock<ICloudinaryService>();
            _fileTypeDetectorMock = new Mock<IFileTypeDetector>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<StoryDetailResponse>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<StoryDetailResponse>> operation, Func<Task>? _) => operation());

            _storyService = new StoryService(
                _storyRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _cloudinaryServiceMock.Object,
                _fileTypeDetectorMock.Object,
                _unitOfWorkMock.Object);
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
                ThumbnailUrl = "  https://cdn.example.com/thumb.jpg  ",
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
            capturedStory.ThumbnailUrl.Should().Be("https://cdn.example.com/thumb.jpg");
            capturedStory.TextContent.Should().BeNull();
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
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
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

            _unitOfWorkMock
                .Setup(x => x.CommitAsync())
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
                ThumbnailUrl = null,
                TextContent = "  hello story  ",
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
            capturedStory.ThumbnailUrl.Should().BeNull();
            capturedStory.TextContent.Should().Be("hello story");
            capturedStory.Privacy.Should().Be(StoryPrivacyEnum.Public); // default when null
            capturedStory.ExpiresAt.Should().BeCloseTo(capturedStory.CreatedAt.AddHours(12), TimeSpan.FromSeconds(1));

            result.MediaUrl.Should().BeNull();
            result.ThumbnailUrl.Should().BeNull();
            result.TextContent.Should().Be("hello story");
            result.Privacy.Should().Be((int)StoryPrivacyEnum.Public);
            _fileTypeDetectorMock.Verify(x => x.GetMediaTypeAsync(It.IsAny<IFormFile>()), Times.Never);
            _cloudinaryServiceMock.Verify(x => x.UploadImageAsync(It.IsAny<IFormFile>()), Times.Never);
            _cloudinaryServiceMock.Verify(x => x.UploadVideoAsync(It.IsAny<IFormFile>()), Times.Never);
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
    }
}
