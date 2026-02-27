using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Moq;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.FollowDTOs;
using CloudM.Application.Services.FollowServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.AccountSettingRepos;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using Xunit;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class FollowServiceTests
    {
        private readonly Mock<IFollowRepository> _mockFollowRepo;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IAccountRepository> _mockAccountRepo;
        private readonly Mock<IAccountSettingRepository> _mockAccountSettingRepo;
        private readonly Mock<IRealtimeService> _mockRealtimeService;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly FollowService _followService;

        public FollowServiceTests()
        {
            _mockFollowRepo = new Mock<IFollowRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockAccountRepo = new Mock<IAccountRepository>();
            _mockAccountSettingRepo = new Mock<IAccountSettingRepository>();
            _mockRealtimeService = new Mock<IRealtimeService>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();

            _followService = new FollowService(
                _mockFollowRepo.Object,
                _mockMapper.Object,
                _mockAccountRepo.Object,
                _mockAccountSettingRepo.Object,
                _mockRealtimeService.Object,
                _mockUnitOfWork.Object
            );
        }

        #region FollowAsync Tests

        [Fact]
        public async Task FollowAsync_WhenSelfFollow_ThrowsBadRequestException()
        {
            // Arrange
            var accountId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _followService.FollowAsync(accountId, accountId));
        }

        [Fact]
        public async Task FollowAsync_WhenTargetNotExist_ThrowsBadRequestException()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _followService.FollowAsync(followerId, targetId));
        }

        [Fact]
        public async Task FollowAsync_WhenFollowerAccountInactive_ThrowsForbiddenException()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(followerId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _followService.FollowAsync(followerId, targetId));
        }

        [Fact]
        public async Task FollowAsync_WhenAlreadyFollowing_ThrowsBadRequestException()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(followerId)).ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(followerId, targetId)).ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _followService.FollowAsync(followerId, targetId));
        }

        [Fact]
        public async Task FollowAsync_WhenValid_CreatesFollowAndReturnsResponse()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(followerId)).ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(followerId, targetId)).ReturnsAsync(false);

            // Setup transaction behavior
            _mockUnitOfWork.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<FollowCountResponse>>>(), It.IsAny<Func<Task>?>()))
                .Returns<Func<Task<FollowCountResponse>>, Func<Task>?>((func, _) => func());

            _mockFollowRepo.Setup(x => x.AddFollowAsync(It.IsAny<Follow>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((10, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((20, 15));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                followerId, targetId, "follow", 10, 5, 20, 15)).Returns(Task.CompletedTask);

            // Act
            var result = await _followService.FollowAsync(followerId, targetId);

            // Assert
            result.Should().NotBeNull();
            result.Followers.Should().Be(10);
            result.Following.Should().Be(5);
            result.IsFollowedByCurrentUser.Should().BeTrue();
        }

        #endregion

        #region UnfollowAsync Tests

        [Fact]
        public async Task UnfollowAsync_WhenNotFollowing_ThrowsBadRequestException()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(followerId, targetId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _followService.UnfollowAsync(followerId, targetId));
        }

        [Fact]
        public async Task UnfollowAsync_WhenFollowing_RemovesFollowAndReturnsResponse()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(followerId, targetId)).ReturnsAsync(true);

            // Setup transaction behavior
            _mockUnitOfWork.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<FollowCountResponse>>>(), It.IsAny<Func<Task>?>()))
                .Returns<Func<Task<FollowCountResponse>>, Func<Task>?>((func, _) => func());

            _mockFollowRepo.Setup(x => x.RemoveFollowAsync(followerId, targetId)).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((9, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((20, 14));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                followerId, targetId, "unfollow", 9, 5, 20, 14)).Returns(Task.CompletedTask);

            // Act
            var result = await _followService.UnfollowAsync(followerId, targetId);

            // Assert
            result.Should().NotBeNull();
            result.Followers.Should().Be(9);
            result.Following.Should().Be(5);
            result.IsFollowedByCurrentUser.Should().BeFalse();
        }

        #endregion

        #region IsFollowingAsync Tests

        [Fact]
        public async Task IsFollowingAsync_WhenFollowing_ReturnsTrue()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockFollowRepo.Setup(x => x.IsFollowingAsync(followerId, targetId)).ReturnsAsync(true);

            // Act
            var result = await _followService.IsFollowingAsync(followerId, targetId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsFollowingAsync_WhenNotFollowing_ReturnsFalse()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockFollowRepo.Setup(x => x.IsFollowingAsync(followerId, targetId)).ReturnsAsync(false);

            // Act
            var result = await _followService.IsFollowingAsync(followerId, targetId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetFollowersAsync Tests

        [Fact]
        public async Task GetFollowersAsync_WhenAccountNotExist_ThrowsNotFoundException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var request = new FollowPagingRequest { Page = 1, PageSize = 10 };

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(accountId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _followService.GetFollowersAsync(accountId, null, request));
        }

        #endregion

        #region GetFollowingAsync Tests

        [Fact]
        public async Task GetFollowingAsync_WhenAccountNotExist_ThrowsNotFoundException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var request = new FollowPagingRequest { Page = 1, PageSize = 10 };

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(accountId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _followService.GetFollowingAsync(accountId, null, request));
        }

        #endregion

        #region GetStatsAsync Tests

        [Fact]
        public async Task GetStatsAsync_ReturnsFollowCounts()
        {
            // Arrange
            var accountId = Guid.NewGuid();

            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(accountId)).ReturnsAsync((100, 50));

            // Act
            var result = await _followService.GetStatsAsync(accountId);

            // Assert
            result.Should().NotBeNull();
            result.Followers.Should().Be(100);
            result.Following.Should().Be(50);
        }

        #endregion
    }
}
