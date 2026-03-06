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
using CloudM.Infrastructure.Repositories.FollowRequests;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using Xunit;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class FollowServiceTests
    {
        private readonly Mock<IFollowRepository> _mockFollowRepo;
        private readonly Mock<IFollowRequestRepository> _mockFollowRequestRepo;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IAccountRepository> _mockAccountRepo;
        private readonly Mock<IAccountSettingRepository> _mockAccountSettingRepo;
        private readonly Mock<IRealtimeService> _mockRealtimeService;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly FollowService _followService;

        public FollowServiceTests()
        {
            _mockFollowRepo = new Mock<IFollowRepository>();
            _mockFollowRequestRepo = new Mock<IFollowRequestRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockAccountRepo = new Mock<IAccountRepository>();
            _mockAccountSettingRepo = new Mock<IAccountSettingRepository>();
            _mockRealtimeService = new Mock<IRealtimeService>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();

            _mockFollowRequestRepo
                .Setup(x => x.IsFollowRequestExistAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(false);
            _mockAccountSettingRepo
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((AccountSettings?)null);

            _followService = new FollowService(
                _mockFollowRepo.Object,
                _mockFollowRequestRepo.Object,
                _mockMapper.Object,
                _mockAccountRepo.Object,
                _mockAccountSettingRepo.Object,
                _mockRealtimeService.Object,
                _mockUnitOfWork.Object
            );
        }

        private void SetupTransactionResult<T>()
        {
            _mockUnitOfWork
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<T>>>(), It.IsAny<Func<Task>?>()))
                .Returns<Func<Task<T>>, Func<Task>?>((func, _) => func());
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
            SetupTransactionResult<FollowCountResponse>();

            _mockFollowRepo.Setup(x => x.AddFollowAsync(It.IsAny<Follow>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((10, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((20, 15));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                followerId, targetId, "follow", 10, 5, 20, 15, null)).Returns(Task.CompletedTask);

            // Act
            var result = await _followService.FollowAsync(followerId, targetId);

            // Assert
            result.Should().NotBeNull();
            result.Followers.Should().Be(10);
            result.Following.Should().Be(5);
            result.IsFollowedByCurrentUser.Should().BeTrue();
        }

        [Fact]
        public async Task FollowAsync_WhenTargetPrivate_CreatesFollowRequestAndReturnsRequestedRelation()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(followerId)).ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(followerId, targetId)).ReturnsAsync(false);
            _mockAccountSettingRepo
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(targetId))
                .ReturnsAsync(new AccountSettings { FollowPrivacy = FollowPrivacyEnum.Private });
            _mockFollowRequestRepo
                .Setup(x => x.IsFollowRequestExistAsync(followerId, targetId))
                .ReturnsAsync(false);
            _mockFollowRequestRepo
                .Setup(x => x.AddFollowRequestAsync(It.IsAny<FollowRequest>()))
                .Returns(Task.CompletedTask);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((11, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((20, 16));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                followerId, targetId, "follow_request", 11, 5, 20, 16, "follow_request_sent")).Returns(Task.CompletedTask);

            SetupTransactionResult<FollowCountResponse>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _followService.FollowAsync(followerId, targetId);

            // Assert
            result.Should().NotBeNull();
            result.IsFollowedByCurrentUser.Should().BeFalse();
            result.IsFollowRequestPendingByCurrentUser.Should().BeTrue();
            result.RelationStatus.Should().Be(FollowRelationStatusEnum.Requested);
            result.TargetFollowPrivacy.Should().Be(FollowPrivacyEnum.Private);
            _mockFollowRequestRepo.Verify(x => x.AddFollowRequestAsync(It.IsAny<FollowRequest>()), Times.Once);
            _mockFollowRepo.Verify(x => x.AddFollowAsync(It.IsAny<Follow>()), Times.Never);
        }

        [Fact]
        public async Task FollowAsync_WhenTargetPrivateAndRequestAlreadyExists_ThrowsBadRequestException()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(followerId)).ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(followerId, targetId)).ReturnsAsync(false);
            _mockAccountSettingRepo
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(targetId))
                .ReturnsAsync(new AccountSettings { FollowPrivacy = FollowPrivacyEnum.Private });
            _mockFollowRequestRepo
                .Setup(x => x.IsFollowRequestExistAsync(followerId, targetId))
                .ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _followService.FollowAsync(followerId, targetId));
        }

        [Fact]
        public async Task FollowAsync_WhenPublicAndPendingRequestExists_RemovesRequestThenCreatesFollow()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(followerId)).ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(followerId, targetId)).ReturnsAsync(false);
            _mockAccountSettingRepo
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(targetId))
                .ReturnsAsync(new AccountSettings { FollowPrivacy = FollowPrivacyEnum.Anyone });
            _mockFollowRequestRepo
                .Setup(x => x.IsFollowRequestExistAsync(followerId, targetId))
                .ReturnsAsync(true);
            _mockFollowRequestRepo
                .Setup(x => x.RemoveFollowRequestAsync(followerId, targetId))
                .Returns(Task.CompletedTask);
            _mockFollowRepo.Setup(x => x.AddFollowAsync(It.IsAny<Follow>())).Returns(Task.CompletedTask);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((12, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((21, 16));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                followerId, targetId, "follow", 12, 5, 21, 16, null)).Returns(Task.CompletedTask);

            SetupTransactionResult<FollowCountResponse>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _followService.FollowAsync(followerId, targetId);

            // Assert
            result.Should().NotBeNull();
            result.IsFollowedByCurrentUser.Should().BeTrue();
            result.IsFollowRequestPendingByCurrentUser.Should().BeFalse();
            result.RelationStatus.Should().Be(FollowRelationStatusEnum.Following);
            _mockFollowRequestRepo.Verify(x => x.RemoveFollowRequestAsync(followerId, targetId), Times.Once);
            _mockFollowRepo.Verify(x => x.AddFollowAsync(It.IsAny<Follow>()), Times.Once);
        }

        #endregion

        #region UnfollowAsync Tests

        [Fact]
        public async Task UnfollowAsync_WhenSelfUnfollow_ThrowsBadRequestException()
        {
            // Arrange
            var accountId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _followService.UnfollowAsync(accountId, accountId));
        }

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
            SetupTransactionResult<FollowCountResponse>();

            _mockFollowRepo.Setup(x => x.RemoveFollowAsync(followerId, targetId)).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((9, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((20, 14));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                followerId, targetId, "unfollow", 9, 5, 20, 14, null)).Returns(Task.CompletedTask);

            // Act
            var result = await _followService.UnfollowAsync(followerId, targetId);

            // Assert
            result.Should().NotBeNull();
            result.Followers.Should().Be(9);
            result.Following.Should().Be(5);
            result.IsFollowedByCurrentUser.Should().BeFalse();
        }

        [Fact]
        public async Task UnfollowAsync_WhenPendingRequestExists_RemovesRequestAndReturnsNotFollowing()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountSettingRepo
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(targetId))
                .ReturnsAsync(new AccountSettings { FollowPrivacy = FollowPrivacyEnum.Private });
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(followerId, targetId)).ReturnsAsync(false);
            _mockFollowRequestRepo.Setup(x => x.IsFollowRequestExistAsync(followerId, targetId)).ReturnsAsync(true);
            _mockFollowRequestRepo.Setup(x => x.RemoveFollowRequestAsync(followerId, targetId)).Returns(Task.CompletedTask);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((9, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((20, 14));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                followerId, targetId, "follow_request_removed", 9, 5, 20, 14, "follow_request_discarded")).Returns(Task.CompletedTask);

            SetupTransactionResult<FollowCountResponse>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _followService.UnfollowAsync(followerId, targetId);

            // Assert
            result.Should().NotBeNull();
            result.IsFollowedByCurrentUser.Should().BeFalse();
            result.IsFollowRequestPendingByCurrentUser.Should().BeFalse();
            result.RelationStatus.Should().Be(FollowRelationStatusEnum.None);
            _mockFollowRequestRepo.Verify(x => x.RemoveFollowRequestAsync(followerId, targetId), Times.Once);
            _mockFollowRepo.Verify(x => x.RemoveFollowAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
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

        #region GetRelationStatusAsync Tests

        [Fact]
        public async Task GetRelationStatusAsync_WhenTargetNotExist_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _followService.GetRelationStatusAsync(currentId, targetId));
        }

        [Fact]
        public async Task GetRelationStatusAsync_WhenPendingRequest_ReturnsRequestedRelation()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((13, 4));
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(currentId, targetId)).ReturnsAsync(false);
            _mockFollowRequestRepo.Setup(x => x.IsFollowRequestExistAsync(currentId, targetId)).ReturnsAsync(true);
            _mockAccountSettingRepo
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(targetId))
                .ReturnsAsync(new AccountSettings { FollowPrivacy = FollowPrivacyEnum.Private });

            // Act
            var result = await _followService.GetRelationStatusAsync(currentId, targetId);

            // Assert
            result.IsFollowedByCurrentUser.Should().BeFalse();
            result.IsFollowRequestPendingByCurrentUser.Should().BeTrue();
            result.RelationStatus.Should().Be(FollowRelationStatusEnum.Requested);
            result.TargetFollowPrivacy.Should().Be(FollowPrivacyEnum.Private);
        }

        #endregion

        #region FollowRequestAction Tests

        [Fact]
        public async Task AcceptFollowRequestAsync_WhenValidAndNotAlreadyFollowing_AddsFollowAndNotifies()
        {
            // Arrange
            var targetId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(requesterId)).ReturnsAsync(true);
            _mockFollowRequestRepo.Setup(x => x.IsFollowRequestExistAsync(requesterId, targetId)).ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(requesterId, targetId)).ReturnsAsync(false);
            _mockFollowRequestRepo.Setup(x => x.RemoveFollowRequestAsync(requesterId, targetId)).Returns(Task.CompletedTask);
            _mockFollowRepo.Setup(x => x.AddFollowAsync(It.IsAny<Follow>())).Returns(Task.CompletedTask);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((15, 7));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(requesterId)).ReturnsAsync((22, 11));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                requesterId, targetId, "follow", 15, 7, 22, 11, "follow_request_accepted")).Returns(Task.CompletedTask);

            SetupTransactionResult<bool>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            await _followService.AcceptFollowRequestAsync(targetId, requesterId);

            // Assert
            _mockFollowRequestRepo.Verify(x => x.RemoveFollowRequestAsync(requesterId, targetId), Times.Once);
            _mockFollowRepo.Verify(x => x.AddFollowAsync(It.IsAny<Follow>()), Times.Once);
            _mockRealtimeService.Verify(x => x.NotifyFollowChangedAsync(
                requesterId, targetId, "follow", 15, 7, 22, 11, "follow_request_accepted"), Times.Once);
        }

        [Fact]
        public async Task RemoveFollowRequestAsync_WhenValid_RemovesRequestAndNotifies()
        {
            // Arrange
            var targetId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockFollowRequestRepo.Setup(x => x.IsFollowRequestExistAsync(requesterId, targetId)).ReturnsAsync(true);
            _mockFollowRequestRepo.Setup(x => x.RemoveFollowRequestAsync(requesterId, targetId)).Returns(Task.CompletedTask);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((14, 7));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(requesterId)).ReturnsAsync((21, 11));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                requesterId, targetId, "follow_request_removed", 14, 7, 21, 11, "follow_request_rejected")).Returns(Task.CompletedTask);

            SetupTransactionResult<bool>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            await _followService.RemoveFollowRequestAsync(targetId, requesterId);

            // Assert
            _mockFollowRequestRepo.Verify(x => x.RemoveFollowRequestAsync(requesterId, targetId), Times.Once);
            _mockRealtimeService.Verify(x => x.NotifyFollowChangedAsync(
                requesterId, targetId, "follow_request_removed", 14, 7, 21, 11, "follow_request_rejected"), Times.Once);
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
