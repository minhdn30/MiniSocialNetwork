using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Moq;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.FollowDTOs;
using CloudM.Application.Services.FollowServices;
using CloudM.Application.Services.NotificationServices;
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
        private readonly Mock<INotificationService> _mockNotificationService;
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
            _mockNotificationService = new Mock<INotificationService>();
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
                _mockNotificationService.Object,
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
            SetupTransactionResult<(bool ShouldNotifyFollowChanged, bool RemovedPendingRequest, (int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();

            _mockFollowRepo
                .Setup(x => x.AddFollowIgnoreExistingAsync(It.IsAny<Follow>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
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
            var requesterAccount = new Account
            {
                AccountId = followerId,
                Username = "requester-user",
                FullName = "requester user",
                AvatarUrl = "https://cdn/requester.jpg"
            };

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(followerId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.GetAccountById(followerId)).ReturnsAsync(requesterAccount);
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(followerId, targetId)).ReturnsAsync(false);
            _mockAccountSettingRepo
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(targetId))
                .ReturnsAsync(new AccountSettings { FollowPrivacy = FollowPrivacyEnum.Private });
            _mockFollowRequestRepo
                .Setup(x => x.IsFollowRequestExistAsync(followerId, targetId))
                .ReturnsAsync(false);
            _mockFollowRequestRepo
                .Setup(x => x.AddFollowRequestIgnoreExistingAsync(It.IsAny<FollowRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
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
            _mockFollowRequestRepo.Verify(x => x.AddFollowRequestIgnoreExistingAsync(It.IsAny<FollowRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockFollowRepo.Verify(x => x.AddFollowAsync(It.IsAny<Follow>()), Times.Never);
            _mockRealtimeService.Verify(x => x.NotifyFollowRequestQueueChangedAsync(
                targetId,
                "upsert",
                followerId,
                It.Is<NotificationToastPayload>(payload =>
                    payload.Type == (int)NotificationTypeEnum.FollowRequest &&
                    payload.ActorAccountId == requesterAccount.AccountId &&
                    payload.ActorUsername == requesterAccount.Username &&
                    payload.ActorFullName == requesterAccount.FullName &&
                    payload.ActorAvatarUrl == requesterAccount.AvatarUrl &&
                    payload.TargetKind == (int)NotificationTargetKindEnum.Account &&
                    payload.TargetId == followerId &&
                    payload.CanOpen)),
                Times.Once);
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
                .Setup(x => x.AddFollowRequestIgnoreExistingAsync(It.IsAny<FollowRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            SetupTransactionResult<FollowCountResponse>();

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
                .Setup(x => x.RemoveFollowRequestAsync(followerId, targetId))
                .ReturnsAsync(1);
            _mockFollowRepo
                .Setup(x => x.AddFollowIgnoreExistingAsync(It.IsAny<Follow>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((12, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((21, 16));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                followerId, targetId, "follow", 12, 5, 21, 16, null)).Returns(Task.CompletedTask);

            SetupTransactionResult<(bool ShouldNotifyFollowChanged, bool RemovedPendingRequest, (int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _followService.FollowAsync(followerId, targetId);

            // Assert
            result.Should().NotBeNull();
            result.IsFollowedByCurrentUser.Should().BeTrue();
            result.IsFollowRequestPendingByCurrentUser.Should().BeFalse();
            result.RelationStatus.Should().Be(FollowRelationStatusEnum.Following);
            _mockFollowRequestRepo.Verify(x => x.RemoveFollowRequestAsync(followerId, targetId), Times.Once);
            _mockFollowRepo.Verify(x => x.AddFollowIgnoreExistingAsync(It.IsAny<Follow>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task FollowAsync_WhenAutoAcceptedDuringRetry_ReturnsFollowingWithoutDuplicateRealtime()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(followerId)).ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(followerId, targetId))
                .ReturnsAsync(false);
            _mockAccountSettingRepo
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(targetId))
                .ReturnsAsync(new AccountSettings { FollowPrivacy = FollowPrivacyEnum.Anyone });
            _mockFollowRequestRepo
                .Setup(x => x.RemoveFollowRequestAsync(followerId, targetId))
                .ReturnsAsync(0);
            _mockFollowRepo
                .Setup(x => x.AddFollowIgnoreExistingAsync(It.IsAny<Follow>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((12, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((21, 16));

            SetupTransactionResult<(bool ShouldNotifyFollowChanged, bool RemovedPendingRequest, (int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _followService.FollowAsync(followerId, targetId);

            // Assert
            result.Should().NotBeNull();
            result.IsFollowedByCurrentUser.Should().BeTrue();
            result.IsFollowRequestPendingByCurrentUser.Should().BeFalse();
            result.RelationStatus.Should().Be(FollowRelationStatusEnum.Following);
            _mockFollowRepo.Verify(x => x.AddFollowIgnoreExistingAsync(It.IsAny<Follow>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockRealtimeService.Verify(x => x.NotifyFollowChangedAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>()), Times.Never);
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
            _mockFollowRepo.Setup(x => x.RemoveFollowAsync(followerId, targetId)).ReturnsAsync(0);
            _mockFollowRequestRepo.Setup(x => x.RemoveFollowRequestAsync(followerId, targetId)).ReturnsAsync(0);

            SetupTransactionResult<(string Action, bool RemovedPendingRequest, (int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();

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
            SetupTransactionResult<(string Action, bool RemovedPendingRequest, (int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();

            _mockFollowRepo.Setup(x => x.RemoveFollowAsync(followerId, targetId)).ReturnsAsync(1);
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
            _mockFollowRepo.Setup(x => x.RemoveFollowAsync(followerId, targetId)).ReturnsAsync(0);
            _mockFollowRequestRepo.Setup(x => x.RemoveFollowRequestAsync(followerId, targetId)).ReturnsAsync(1);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((9, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((20, 14));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                followerId, targetId, "follow_request_removed", 9, 5, 20, 14, "follow_request_discarded")).Returns(Task.CompletedTask);

            SetupTransactionResult<(string Action, bool RemovedPendingRequest, (int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _followService.UnfollowAsync(followerId, targetId);

            // Assert
            result.Should().NotBeNull();
            result.IsFollowedByCurrentUser.Should().BeFalse();
            result.IsFollowRequestPendingByCurrentUser.Should().BeFalse();
            result.RelationStatus.Should().Be(FollowRelationStatusEnum.None);
            _mockFollowRequestRepo.Verify(x => x.RemoveFollowRequestAsync(followerId, targetId), Times.Once);
            _mockFollowRepo.Verify(x => x.RemoveFollowAsync(followerId, targetId), Times.Once);
        }

        [Fact]
        public async Task UnfollowAsync_WhenRequestStateChangedToFollow_RemovesFollowAndReturnsNotFollowing()
        {
            // Arrange
            var followerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountSettingRepo
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(targetId))
                .ReturnsAsync(new AccountSettings { FollowPrivacy = FollowPrivacyEnum.Private });
            _mockFollowRepo.Setup(x => x.RemoveFollowAsync(followerId, targetId)).ReturnsAsync(1);
            _mockFollowRequestRepo.Setup(x => x.RemoveFollowRequestAsync(followerId, targetId)).ReturnsAsync(0);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((9, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((20, 14));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                followerId, targetId, "unfollow", 9, 5, 20, 14, null)).Returns(Task.CompletedTask);

            SetupTransactionResult<(string Action, bool RemovedPendingRequest, (int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _followService.UnfollowAsync(followerId, targetId);

            // Assert
            result.Should().NotBeNull();
            result.IsFollowedByCurrentUser.Should().BeFalse();
            result.IsFollowRequestPendingByCurrentUser.Should().BeFalse();
            result.RelationStatus.Should().Be(FollowRelationStatusEnum.None);
            _mockFollowRepo.Verify(x => x.RemoveFollowAsync(followerId, targetId), Times.Once);
            _mockFollowRequestRepo.Verify(x => x.RemoveFollowRequestAsync(followerId, targetId), Times.Once);
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
            _mockFollowRequestRepo.Setup(x => x.RemoveFollowRequestAsync(requesterId, targetId)).ReturnsAsync(1);
            _mockFollowRepo
                .Setup(x => x.AddFollowIgnoreExistingAsync(It.IsAny<Follow>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((15, 7));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(requesterId)).ReturnsAsync((22, 11));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                requesterId, targetId, "follow", 15, 7, 22, 11, "follow_request_accepted")).Returns(Task.CompletedTask);

            SetupTransactionResult<(bool ShouldNotifyAccepted, (int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            await _followService.AcceptFollowRequestAsync(targetId, requesterId);

            // Assert
            _mockFollowRequestRepo.Verify(x => x.RemoveFollowRequestAsync(requesterId, targetId), Times.Once);
            _mockFollowRepo.Verify(x => x.AddFollowIgnoreExistingAsync(It.IsAny<Follow>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockRealtimeService.Verify(x => x.NotifyFollowChangedAsync(
                requesterId, targetId, "follow", 15, 7, 22, 11, "follow_request_accepted"), Times.Once);
        }

        [Fact]
        public async Task AcceptFollowRequestAsync_WhenFollowInsertedConcurrentlyAfterRequestRemoval_DoesNotNotifyAgain()
        {
            // Arrange
            var targetId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(requesterId)).ReturnsAsync(true);
            _mockFollowRequestRepo.Setup(x => x.RemoveFollowRequestAsync(requesterId, targetId)).ReturnsAsync(1);
            _mockFollowRepo
                .Setup(x => x.AddFollowIgnoreExistingAsync(It.IsAny<Follow>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((15, 7));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(requesterId)).ReturnsAsync((22, 11));

            SetupTransactionResult<(bool ShouldNotifyAccepted, (int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            await _followService.AcceptFollowRequestAsync(targetId, requesterId);

            // Assert
            _mockRealtimeService.Verify(x => x.NotifyFollowChangedAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task AcceptFollowRequestAsync_WhenAlreadyProcessed_DoesNotNotifyAgain()
        {
            // Arrange
            var targetId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockAccountRepo.Setup(x => x.IsAccountIdExist(requesterId)).ReturnsAsync(true);
            _mockFollowRequestRepo.Setup(x => x.RemoveFollowRequestAsync(requesterId, targetId)).ReturnsAsync(0);
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(requesterId, targetId)).ReturnsAsync(true);

            SetupTransactionResult<(bool ShouldNotifyAccepted, (int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();

            // Act
            await _followService.AcceptFollowRequestAsync(targetId, requesterId);

            // Assert
            _mockRealtimeService.Verify(x => x.NotifyFollowChangedAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task RemoveFollowRequestAsync_WhenValid_RemovesRequestAndNotifies()
        {
            // Arrange
            var targetId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockFollowRequestRepo.Setup(x => x.IsFollowRequestExistAsync(requesterId, targetId)).ReturnsAsync(true);
            _mockFollowRequestRepo.Setup(x => x.RemoveFollowRequestAsync(requesterId, targetId)).ReturnsAsync(1);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(targetId)).ReturnsAsync((14, 7));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(requesterId)).ReturnsAsync((21, 11));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                requesterId, targetId, "follow_request_removed", 14, 7, 21, 11, "follow_request_rejected")).Returns(Task.CompletedTask);

            SetupTransactionResult<((int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            await _followService.RemoveFollowRequestAsync(targetId, requesterId);

            // Assert
            _mockFollowRequestRepo.Verify(x => x.RemoveFollowRequestAsync(requesterId, targetId), Times.Once);
            _mockRealtimeService.Verify(x => x.NotifyFollowChangedAsync(
                requesterId, targetId, "follow_request_removed", 14, 7, 21, 11, "follow_request_rejected"), Times.Once);
        }

        [Fact]
        public async Task RemoveFollowRequestAsync_WhenAlreadyProcessedAsFollow_ThrowsBadRequestAndDoesNotNotify()
        {
            // Arrange
            var targetId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(targetId)).ReturnsAsync(true);
            _mockFollowRequestRepo.Setup(x => x.RemoveFollowRequestAsync(requesterId, targetId)).ReturnsAsync(0);
            _mockFollowRepo.Setup(x => x.IsFollowRecordExistAsync(requesterId, targetId)).ReturnsAsync(true);

            SetupTransactionResult<((int Followers, int Following) TargetCounts, (int Followers, int Following) CurrentCounts)>();

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _followService.RemoveFollowRequestAsync(targetId, requesterId));

            _mockRealtimeService.Verify(x => x.NotifyFollowChangedAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>()), Times.Never);
        }

        #endregion

        #region RemoveFollowerAsync Tests

        [Fact]
        public async Task RemoveFollowerAsync_WhenFollowExists_RemovesFollowerAndNotifies()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var followerId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(currentId)).ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.RemoveFollowAsync(followerId, currentId)).ReturnsAsync(1);
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(currentId)).ReturnsAsync((8, 5));
            _mockFollowRepo.Setup(x => x.GetFollowCountsAsync(followerId)).ReturnsAsync((14, 10));
            _mockRealtimeService.Setup(x => x.NotifyFollowChangedAsync(
                followerId, currentId, "remove_follower", 8, 5, 14, 10, null)).Returns(Task.CompletedTask);

            SetupTransactionResult<(bool Removed, (int Followers, int Following) CurrentCounts, (int Followers, int Following) FollowerCounts)>();
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            await _followService.RemoveFollowerAsync(currentId, followerId);

            // Assert
            _mockFollowRepo.Verify(x => x.RemoveFollowAsync(followerId, currentId), Times.Once);
            _mockNotificationService.Verify(x => x.EnqueueAggregateEventAsync(
                It.Is<NotificationAggregateEvent>(evt =>
                    evt.RecipientId == currentId &&
                    evt.Type == NotificationTypeEnum.Follow &&
                    evt.AggregateKey == NotificationAggregateKeys.Follow(followerId) &&
                    evt.Action == NotificationAggregateActionEnum.Deactivate &&
                    evt.SourceId == followerId),
                It.IsAny<CancellationToken>()), Times.Once);
            _mockNotificationService.Verify(x => x.EnqueueAggregateEventAsync(
                It.Is<NotificationAggregateEvent>(evt =>
                    evt.RecipientId == currentId &&
                    evt.Type == NotificationTypeEnum.Follow &&
                    evt.AggregateKey == NotificationAggregateKeys.FollowAutoAcceptSummary(currentId) &&
                    evt.Action == NotificationAggregateActionEnum.Deactivate &&
                    evt.SourceId == followerId),
                It.IsAny<CancellationToken>()), Times.Once);
            _mockRealtimeService.Verify(x => x.NotifyFollowChangedAsync(
                followerId, currentId, "remove_follower", 8, 5, 14, 10, null), Times.Once);
        }

        [Fact]
        public async Task RemoveFollowerAsync_WhenAlreadyRemoved_DoesNotNotify()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var followerId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(currentId)).ReturnsAsync(true);
            _mockFollowRepo.Setup(x => x.RemoveFollowAsync(followerId, currentId)).ReturnsAsync(0);

            SetupTransactionResult<(bool Removed, (int Followers, int Following) CurrentCounts, (int Followers, int Following) FollowerCounts)>();

            // Act
            await _followService.RemoveFollowerAsync(currentId, followerId);

            // Assert
            _mockNotificationService.Verify(x => x.EnqueueAggregateEventAsync(
                It.IsAny<NotificationAggregateEvent>(),
                It.IsAny<CancellationToken>()), Times.Never);
            _mockRealtimeService.Verify(x => x.NotifyFollowChangedAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>()), Times.Never);
        }

        #endregion

        #region GetSentPendingRequestsAsync Tests

        [Fact]
        public async Task GetSentPendingRequestsAsync_WhenCurrentAccountMissing_ThrowsForbiddenException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var request = new FollowPagingRequest
            {
                Page = 1,
                PageSize = 15
            };

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(currentId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _followService.GetSentPendingRequestsAsync(currentId, request));
        }

        [Fact]
        public async Task GetSentPendingRequestsAsync_WhenValid_ReturnsPagedResponse()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var request = new FollowPagingRequest
            {
                Keyword = "alex",
                SortByCreatedASC = null,
                Page = 1,
                PageSize = 15
            };
            var items = new List<AccountWithFollowStatusModel>
            {
                new()
                {
                    AccountId = Guid.NewGuid(),
                    Username = "alex.dev",
                    FullName = "Alex Dev",
                    AvatarUrl = "/avatars/alex.png",
                    IsFollowing = false,
                    IsFollowRequested = true,
                    IsFollower = false
                }
            };

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(currentId)).ReturnsAsync(true);
            _mockFollowRequestRepo
                .Setup(x => x.GetPendingSentByRequesterAsync(currentId, "alex", null, 1, 15))
                .ReturnsAsync((items, 1));

            // Act
            var result = await _followService.GetSentPendingRequestsAsync(currentId, request);

            // Assert
            result.Should().NotBeNull();
            result.TotalItems.Should().Be(1);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(15);
            result.Items.Should().ContainSingle();
            result.Items.First().Username.Should().Be("alex.dev");
            result.Items.First().IsFollowRequested.Should().BeTrue();
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

        #region GetSuggestionsAsync Tests

        [Fact]
        public async Task GetSuggestionsAsync_WhenCurrentAccountMissing_ThrowsForbiddenException()
        {
            // Arrange
            var currentId = Guid.NewGuid();

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(currentId)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _followService.GetSuggestionsAsync(currentId, new FollowSuggestionPagingRequest()));
        }

        [Fact]
        public async Task GetSuggestionsAsync_WhenHomeSurfaceRequested_NormalizesRequestAndUsesDiscoveryMode()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var items = new List<FollowSuggestionModel>
            {
                new()
                {
                    AccountId = Guid.NewGuid(),
                    Username = "home-user",
                    FullName = "Home User",
                    AvatarUrl = "/avatars/home-user.png"
                }
            };

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(currentId)).ReturnsAsync(true);
            _mockAccountRepo
                .Setup(x => x.GetFollowSuggestionsAsync(currentId, 1, 5, true))
                .ReturnsAsync((items, 1));

            var request = new FollowSuggestionPagingRequest
            {
                Page = 0,
                PageSize = 0,
                Surface = "HOME"
            };

            // Act
            var result = await _followService.GetSuggestionsAsync(currentId, request);

            // Assert
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(5);
            result.TotalItems.Should().Be(1);
            result.Items.Should().ContainSingle();
            _mockAccountRepo.Verify(x => x.GetFollowSuggestionsAsync(currentId, 1, 5, true), Times.Once);
        }

        [Fact]
        public async Task GetSuggestionsAsync_WhenPageSurfaceRequested_CapsPageSizeAtTwenty()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var items = new List<FollowSuggestionModel>
            {
                new()
                {
                    AccountId = Guid.NewGuid(),
                    Username = "page-user",
                    FullName = "Page User",
                    AvatarUrl = "/avatars/page-user.png"
                }
            };

            _mockAccountRepo.Setup(x => x.IsAccountIdExist(currentId)).ReturnsAsync(true);
            _mockAccountRepo
                .Setup(x => x.GetFollowSuggestionsAsync(currentId, 2, 20, false))
                .ReturnsAsync((items, 25));

            var request = new FollowSuggestionPagingRequest
            {
                Page = 2,
                PageSize = 200,
                Surface = "page"
            };

            // Act
            var result = await _followService.GetSuggestionsAsync(currentId, request);

            // Assert
            result.Page.Should().Be(2);
            result.PageSize.Should().Be(20);
            result.TotalItems.Should().Be(25);
            result.Items.Should().ContainSingle();
            _mockAccountRepo.Verify(x => x.GetFollowSuggestionsAsync(currentId, 2, 20, false), Times.Once);
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
