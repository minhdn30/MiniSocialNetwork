using AutoMapper;
using FluentAssertions;
using Moq;
using CloudM.Application.DTOs.StoryDTOs;
using CloudM.Application.Services.StoryViewServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Stories;
using CloudM.Infrastructure.Repositories.StoryViews;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class StoryViewServiceTests
    {
        private readonly Mock<IStoryViewRepository> _storyViewRepositoryMock;
        private readonly Mock<IStoryRepository> _storyRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly StoryViewService _storyViewService;

        public StoryViewServiceTests()
        {
            _storyViewRepositoryMock = new Mock<IStoryViewRepository>();
            _storyRepositoryMock = new Mock<IStoryRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _mapperMock = new Mock<IMapper>();

            _mapperMock
                .Setup(x => x.Map<StoryActiveItemResponse>(It.IsAny<StoryActiveItemModel>()))
                .Returns<StoryActiveItemModel>(model => new StoryActiveItemResponse
                {
                    StoryId = model.StoryId,
                    ContentType = (int)model.ContentType,
                    MediaUrl = model.MediaUrl,
                    TextContent = model.TextContent,
                    BackgroundColorKey = model.BackgroundColorKey,
                    FontTextKey = model.FontTextKey,
                    FontSizeKey = model.FontSizeKey,
                    TextColorKey = model.TextColorKey,
                    Privacy = (int)model.Privacy,
                    CreatedAt = model.CreatedAt,
                    ExpiresAt = model.ExpiresAt,
                    IsViewedByCurrentUser = model.IsViewedByCurrentUser,
                    CurrentUserReactType = model.CurrentUserReactType.HasValue
                        ? (int)model.CurrentUserReactType.Value
                        : null
                });

            _mapperMock
                .Setup(x => x.Map<List<StoryViewerBasicResponse>>(It.IsAny<List<StoryViewerBasicModel>>()))
                .Returns<List<StoryViewerBasicModel>>(items =>
                    items.Select(x => new StoryViewerBasicResponse
                    {
                        AccountId = x.AccountId,
                        Username = x.Username,
                        FullName = x.FullName,
                        AvatarUrl = x.AvatarUrl,
                        ViewedAt = x.ViewedAt,
                        ReactType = x.ReactType
                    }).ToList());

            _storyViewService = new StoryViewService(
                _storyViewRepositoryMock.Object,
                _storyRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _mapperMock.Object);
        }

        [Fact]
        public async Task ReactStoryAsync_WhenStoryNotViewable_ThrowsNotFoundException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var request = new StoryReactRequest { ReactType = (int)ReactEnum.Like };

            _storyRepositoryMock
                .Setup(x => x.GetViewableStoryIdsAsync(
                    currentId,
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(storyId)),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Guid>());

            // Act
            var act = () => _storyViewService.ReactStoryAsync(currentId, storyId, request);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("Story not found or expired.");
        }

        [Fact]
        public async Task ReactStoryAsync_WhenStoryExpired_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var request = new StoryReactRequest { ReactType = (int)ReactEnum.Like };
            var story = new Story
            {
                StoryId = storyId,
                AccountId = Guid.NewGuid(),
                ContentType = StoryContentTypeEnum.Image,
                MediaUrl = "https://cdn.example.com/story.jpg",
                Privacy = StoryPrivacyEnum.Public,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
                IsDeleted = false
            };

            _storyRepositoryMock
                .Setup(x => x.GetStoryByIdAsync(storyId))
                .ReturnsAsync(story);

            // Act
            var act = () => _storyViewService.ReactStoryAsync(currentId, storyId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Story has expired.");
        }

        [Fact]
        public async Task MarkStoriesViewedAsync_WhenTooManyStoryIds_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var request = new StoryMarkViewedRequest
            {
                StoryIds = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList()
            };

            // Act
            var act = () => _storyViewService.MarkStoriesViewedAsync(currentId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Maximum 200 storyIds are allowed per request.");
        }

        [Fact]
        public async Task MarkStoriesViewedAsync_WhenConcurrentConflict_ReturnsActualMarkedCount()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var storyId1 = Guid.NewGuid();
            var storyId2 = Guid.NewGuid();
            var request = new StoryMarkViewedRequest
            {
                StoryIds = new List<Guid> { storyId1, storyId2 }
            };

            _storyRepositoryMock
                .Setup(x => x.GetViewableStoryIdsAsync(
                    currentId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Guid> { storyId1, storyId2 });

            _storyViewRepositoryMock
                .Setup(x => x.GetViewedStoryIdsByViewerAsync(
                    currentId,
                    It.IsAny<IReadOnlyCollection<Guid>>()))
                .ReturnsAsync(new HashSet<Guid>());

            _storyViewRepositoryMock
                .Setup(x => x.AddStoryViewsIgnoreConflictAsync(It.IsAny<IEnumerable<StoryView>>()))
                .ReturnsAsync(1);

            // Act
            var result = await _storyViewService.MarkStoriesViewedAsync(currentId, request);

            // Assert
            result.RequestedCount.Should().Be(2);
            result.VisibleCount.Should().Be(2);
            result.MarkedCount.Should().Be(1);
            _storyViewRepositoryMock.Verify(
                x => x.AddStoryViewsIgnoreConflictAsync(It.IsAny<IEnumerable<StoryView>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetStoryViewersAsync_WhenInvalidPagination_NormalizesPaging()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var story = new Story
            {
                StoryId = storyId,
                AccountId = currentId,
                IsDeleted = false
            };

            _storyRepositoryMock
                .Setup(x => x.GetStoryByIdAsync(storyId))
                .ReturnsAsync(story);

            _storyViewRepositoryMock
                .Setup(x => x.GetStoryViewersPagedAsync(storyId, 1, 20))
                .ReturnsAsync((new List<StoryViewerBasicModel>(), 0));

            // Act
            var result = await _storyViewService.GetStoryViewersAsync(currentId, storyId, 0, -5);

            // Assert
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(20);
            _storyViewRepositoryMock.Verify(x => x.GetStoryViewersPagedAsync(storyId, 1, 20), Times.Once);
        }

        [Fact]
        public async Task ReactStoryAsync_WhenConcurrentInsertOccurs_FallbacksToToggleLogic()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var request = new StoryReactRequest { ReactType = (int)ReactEnum.Like };
            var story = new Story
            {
                StoryId = storyId,
                AccountId = Guid.NewGuid(),
                ContentType = StoryContentTypeEnum.Image,
                MediaUrl = "https://cdn.example.com/story.jpg",
                Privacy = StoryPrivacyEnum.Public,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsDeleted = false
            };

            _storyRepositoryMock
                .Setup(x => x.GetViewableStoryIdsAsync(
                    currentId,
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(storyId)),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Guid> { storyId });

            _storyRepositoryMock
                .Setup(x => x.GetStoryByIdAsync(storyId))
                .ReturnsAsync(story);

            _storyViewRepositoryMock
                .SetupSequence(x => x.GetStoryViewAsync(storyId, currentId))
                .ReturnsAsync((StoryView?)null)
                .ReturnsAsync(new StoryView
                {
                    StoryId = storyId,
                    ViewerAccountId = currentId,
                    ViewedAt = DateTime.UtcNow.AddSeconds(-10),
                    ReactType = ReactEnum.Like,
                    ReactedAt = DateTime.UtcNow.AddSeconds(-10)
                });

            _storyViewRepositoryMock
                .Setup(x => x.TryAddStoryViewAsync(It.IsAny<StoryView>()))
                .ReturnsAsync(false);

            _storyViewRepositoryMock
                .Setup(x => x.UpdateStoryViewAsync(It.IsAny<StoryView>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.CommitAsync())
                .Returns(Task.CompletedTask);

            // Act
            var result = await _storyViewService.ReactStoryAsync(currentId, storyId, request);

            // Assert
            result.CurrentUserReactType.Should().BeNull();
            _storyViewRepositoryMock.Verify(x => x.UpdateStoryViewAsync(It.IsAny<StoryView>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }
    }
}
