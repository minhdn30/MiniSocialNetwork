using FluentAssertions;
using Moq;
using SocialNetwork.Application.DTOs.StoryHighlightDTOs;
using SocialNetwork.Application.Services.StoryHighlightServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.AccountSettingRepos;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.StoryHighlights;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Tests.Services
{
    public class StoryHighlightServiceTests
    {
        private readonly Mock<IStoryHighlightRepository> _storyHighlightRepositoryMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IAccountSettingRepository> _accountSettingRepositoryMock;
        private readonly Mock<IFollowRepository> _followRepositoryMock;
        private readonly Mock<ICloudinaryService> _cloudinaryServiceMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly StoryHighlightService _service;

        public StoryHighlightServiceTests()
        {
            _storyHighlightRepositoryMock = new Mock<IStoryHighlightRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _accountSettingRepositoryMock = new Mock<IAccountSettingRepository>();
            _followRepositoryMock = new Mock<IFollowRepository>();
            _cloudinaryServiceMock = new Mock<ICloudinaryService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<StoryHighlightGroupMutationResponse>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<StoryHighlightGroupMutationResponse>> operation, Func<Task>? _) => operation());

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<bool>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<bool>> operation, Func<Task>? _) => operation());
            _unitOfWorkMock
                .Setup(x => x.CommitAsync())
                .Returns(Task.CompletedTask);

            _service = new StoryHighlightService(
                _storyHighlightRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _accountSettingRepositoryMock.Object,
                _followRepositoryMock.Object,
                _cloudinaryServiceMock.Object,
                _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task GetProfileHighlightGroupsAsync_WhenTargetPrivacyPrivateAndNotOwner_ReturnsEmpty()
        {
            // Arrange
            var targetId = Guid.NewGuid();
            var currentId = Guid.NewGuid();

            _accountRepositoryMock
                .Setup(x => x.IsAccountIdExist(targetId))
                .ReturnsAsync(true);
            _accountSettingRepositoryMock
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(targetId))
                .ReturnsAsync(new AccountSettings
                {
                    AccountId = targetId,
                    StoryHighlightPrivacy = AccountPrivacyEnum.Private
                });

            // Act
            var result = await _service.GetProfileHighlightGroupsAsync(targetId, currentId);

            // Assert
            result.Should().BeEmpty();
            _storyHighlightRepositoryMock.Verify(x => x.GetHighlightGroupsByOwnerAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task CreateGroupAsync_WhenReachedMaxGroups_ThrowsBadRequest()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var request = new StoryHighlightCreateGroupRequest
            {
                Name = "Trip",
                StoryIds = new List<Guid> { Guid.NewGuid() }
            };

            _storyHighlightRepositoryMock
                .Setup(x => x.CountGroupsByOwnerAsync(currentId))
                .ReturnsAsync(20);

            // Act
            var act = () => _service.CreateGroupAsync(currentId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Maximum 20 highlight groups are allowed.");
        }

        [Fact]
        public async Task CreateGroupAsync_WhenValidRequest_AddsGroupAndItems()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var storyId1 = Guid.NewGuid();
            var storyId2 = Guid.NewGuid();
            var request = new StoryHighlightCreateGroupRequest
            {
                Name = "  Memories  ",
                StoryIds = new List<Guid> { storyId2, storyId1 }
            };

            _storyHighlightRepositoryMock
                .Setup(x => x.CountGroupsByOwnerAsync(currentId))
                .ReturnsAsync(0);
            _storyHighlightRepositoryMock
                .Setup(x => x.GetArchiveStoriesByIdsForOwnerAsync(
                    currentId,
                    It.Is<IReadOnlyCollection<Guid>>(ids =>
                        ids.Count == 2 &&
                        ids.Contains(storyId1) &&
                        ids.Contains(storyId2)),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(new List<StoryHighlightArchiveCandidateModel>
                {
                    new StoryHighlightArchiveCandidateModel
                    {
                        StoryId = storyId1,
                        ContentType = StoryContentTypeEnum.Text,
                        CreatedAt = DateTime.UtcNow.AddDays(-2),
                        ExpiresAt = DateTime.UtcNow.AddDays(-1)
                    },
                    new StoryHighlightArchiveCandidateModel
                    {
                        StoryId = storyId2,
                        ContentType = StoryContentTypeEnum.Image,
                        CreatedAt = DateTime.UtcNow.AddDays(-1),
                        ExpiresAt = DateTime.UtcNow.AddHours(-1)
                    }
                });

            StoryHighlightGroup? capturedGroup = null;
            List<StoryHighlightItem>? capturedItems = null;

            _storyHighlightRepositoryMock
                .Setup(x => x.AddGroupAsync(It.IsAny<StoryHighlightGroup>()))
                .Callback<StoryHighlightGroup>(group => capturedGroup = group)
                .Returns(Task.CompletedTask);
            _storyHighlightRepositoryMock
                .Setup(x => x.AddItemsAsync(It.IsAny<IEnumerable<StoryHighlightItem>>()))
                .Callback<IEnumerable<StoryHighlightItem>>(items => capturedItems = items.ToList())
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.CreateGroupAsync(currentId, request);

            // Assert
            capturedGroup.Should().NotBeNull();
            capturedGroup!.AccountId.Should().Be(currentId);
            capturedGroup.Name.Should().Be("Memories");

            capturedItems.Should().NotBeNull();
            capturedItems!.Should().HaveCount(2);
            capturedItems.Select(x => x.StoryId).Should().ContainInOrder(storyId1, storyId2);

            result.Name.Should().Be("Memories");
            result.StoryCount.Should().Be(2);
            _unitOfWorkMock.Verify(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<StoryHighlightGroupMutationResponse>>>(),
                It.IsAny<Func<Task>?>()), Times.Once);
        }

        [Fact]
        public async Task RemoveItemAsync_WhenRemovingLastItem_DeletesGroupAndCover()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var storyId = Guid.NewGuid();

            var group = new StoryHighlightGroup
            {
                StoryHighlightGroupId = groupId,
                AccountId = currentId,
                Name = "test",
                CoverImageUrl = "https://res.cloudinary.com/demo/image/upload/v1/old-cover.jpg"
            };

            _storyHighlightRepositoryMock
                .Setup(x => x.GetGroupByIdByOwnerAsync(groupId, currentId))
                .ReturnsAsync(group);
            _storyHighlightRepositoryMock
                .Setup(x => x.GetExistingStoryIdsInGroupAsync(groupId, It.IsAny<IReadOnlyCollection<Guid>>()))
                .ReturnsAsync(new HashSet<Guid> { storyId });
            _storyHighlightRepositoryMock
                .Setup(x => x.CountEffectiveStoriesInGroupAsync(groupId))
                .ReturnsAsync(1);
            _storyHighlightRepositoryMock
                .Setup(x => x.TryRemoveGroupIfEffectivelyEmptyAsync(groupId, currentId))
                .ReturnsAsync(true);

            _cloudinaryServiceMock
                .Setup(x => x.GetPublicIdFromUrl(group.CoverImageUrl!))
                .Returns("old-cover");
            _cloudinaryServiceMock
                .Setup(x => x.DeleteMediaAsync("old-cover", MediaTypeEnum.Image))
                .ReturnsAsync(true);

            // Act
            await _service.RemoveItemAsync(currentId, groupId, storyId);

            // Assert
            _storyHighlightRepositoryMock.Verify(x => x.RemoveItemAsync(groupId, storyId), Times.Once);
            _storyHighlightRepositoryMock.Verify(x => x.TryRemoveGroupIfEffectivelyEmptyAsync(groupId, currentId), Times.Once);
            _storyHighlightRepositoryMock.Verify(x => x.UpdateGroupAsync(It.IsAny<StoryHighlightGroup>()), Times.Never);
            _cloudinaryServiceMock.Verify(x => x.DeleteMediaAsync("old-cover", MediaTypeEnum.Image), Times.Once);
        }

        [Fact]
        public async Task AddItemsAsync_WhenGroupIsEffectivelyEmpty_ThrowsNotFoundException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var request = new StoryHighlightAddItemsRequest
            {
                StoryIds = new List<Guid> { Guid.NewGuid() }
            };

            _storyHighlightRepositoryMock
                .Setup(x => x.GetGroupByIdByOwnerAsync(groupId, currentId))
                .ReturnsAsync(new StoryHighlightGroup
                {
                    StoryHighlightGroupId = groupId,
                    AccountId = currentId,
                    Name = "Empty Group"
                });
            _storyHighlightRepositoryMock
                .Setup(x => x.CountEffectiveStoriesInGroupAsync(groupId))
                .ReturnsAsync(0);

            // Act
            var act = () => _service.AddItemsAsync(currentId, groupId, request);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("Highlight group not found.");
        }
    }
}
