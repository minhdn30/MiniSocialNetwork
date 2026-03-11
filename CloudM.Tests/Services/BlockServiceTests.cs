using FluentAssertions;
using Moq;
using CloudM.Application.DTOs.BlockDTOs;
using CloudM.Application.Services.BlockServices;
using CloudM.Application.Services.NotificationServices;
using CloudM.Application.Services.PresenceServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.FollowRequests;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class BlockServiceTests
    {
        private readonly Mock<IAccountBlockRepository> _accountBlockRepositoryMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IFollowRepository> _followRepositoryMock;
        private readonly Mock<IFollowRequestRepository> _followRequestRepositoryMock;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly Mock<IOnlinePresenceService> _onlinePresenceServiceMock;
        private readonly Mock<IRealtimeService> _realtimeServiceMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly BlockService _blockService;

        public BlockServiceTests()
        {
            _accountBlockRepositoryMock = new Mock<IAccountBlockRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _followRepositoryMock = new Mock<IFollowRepository>();
            _followRequestRepositoryMock = new Mock<IFollowRequestRepository>();
            _notificationServiceMock = new Mock<INotificationService>();
            _onlinePresenceServiceMock = new Mock<IOnlinePresenceService>();
            _realtimeServiceMock = new Mock<IRealtimeService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _notificationServiceMock
                .Setup(x => x.EnqueueAggregateEventAsync(
                    It.IsAny<NotificationAggregateEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _onlinePresenceServiceMock
                .Setup(x => x.NotifyBlockedPairHiddenAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _realtimeServiceMock
                .Setup(x => x.NotifyFollowChangedAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>()))
                .Returns(Task.CompletedTask);
            _realtimeServiceMock
                .Setup(x => x.NotifyFollowRequestQueueChangedAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<NotificationToastPayload?>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<bool>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<bool>> operation, Func<Task>? _) => operation());

            _blockService = new BlockService(
                _accountBlockRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _followRepositoryMock.Object,
                _followRequestRepositoryMock.Object,
                _notificationServiceMock.Object,
                _onlinePresenceServiceMock.Object,
                _realtimeServiceMock.Object,
                _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task BlockAsync_ValidRequest_RemovesRelationshipsAndReturnsBlockedStatus()
        {
            // arrange
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var currentAccount = new Account
            {
                AccountId = currentId,
                Username = "current-user"
            };
            var targetAccount = new Account
            {
                AccountId = targetId,
                Username = "target-user"
            };

            var isBlockedByCurrentUser = false;
            AccountBlock? capturedBlock = null;

            _accountRepositoryMock
                .Setup(x => x.GetAccountById(currentId))
                .ReturnsAsync(currentAccount);
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(targetId))
                .ReturnsAsync(targetAccount);

            _accountBlockRepositoryMock
                .Setup(x => x.AddIgnoreExistingAsync(It.IsAny<AccountBlock>(), It.IsAny<CancellationToken>()))
                .Callback<AccountBlock, CancellationToken>((block, _) =>
                {
                    capturedBlock = block;
                    isBlockedByCurrentUser = true;
                })
                .ReturnsAsync(true);

            _accountBlockRepositoryMock
                .Setup(x => x.GetRelationsAsync(currentId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                    isBlockedByCurrentUser
                        ? new List<AccountBlockRelationModel>
                        {
                            new()
                            {
                                TargetId = targetId,
                                IsBlockedByCurrentUser = true,
                                IsBlockedByTargetUser = false
                            }
                        }
                        : new List<AccountBlockRelationModel>());

            _followRepositoryMock
                .SetupSequence(x => x.RemoveFollowAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(1)
                .ReturnsAsync(0);
            _followRequestRepositoryMock
                .SetupSequence(x => x.RemoveFollowRequestAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(1)
                .ReturnsAsync(0);
            _followRepositoryMock
                .Setup(x => x.GetFollowCountsAsync(currentId))
                .ReturnsAsync((3, 4));
            _followRepositoryMock
                .Setup(x => x.GetFollowCountsAsync(targetId))
                .ReturnsAsync((5, 6));

            // act
            var result = await _blockService.BlockAsync(currentId, targetId);

            // assert
            result.TargetId.Should().Be(targetId);
            result.IsBlockedByCurrentUser.Should().BeTrue();
            result.IsBlockedByTargetUser.Should().BeFalse();
            result.IsBlockedEitherWay.Should().BeTrue();

            capturedBlock.Should().NotBeNull();
            capturedBlock!.BlockerId.Should().Be(currentId);
            capturedBlock.BlockedId.Should().Be(targetId);
            capturedBlock.BlockerSnapshotUsername.Should().Be(currentAccount.Username);
            capturedBlock.BlockedSnapshotUsername.Should().Be(targetAccount.Username);

            _unitOfWorkMock.Verify(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<Func<Task>?>()), Times.Once);
            _followRepositoryMock.Verify(x => x.RemoveFollowAsync(currentId, targetId), Times.Once);
            _followRepositoryMock.Verify(x => x.RemoveFollowAsync(targetId, currentId), Times.Once);
            _followRequestRepositoryMock.Verify(x => x.RemoveFollowRequestAsync(currentId, targetId), Times.Once);
            _followRequestRepositoryMock.Verify(x => x.RemoveFollowRequestAsync(targetId, currentId), Times.Once);
            _notificationServiceMock.Verify(x => x.EnqueueAggregateEventAsync(
                It.Is<NotificationAggregateEvent>(evt =>
                    evt.RecipientId == targetId &&
                    evt.Action == NotificationAggregateActionEnum.Deactivate &&
                    evt.Type == NotificationTypeEnum.Follow &&
                    evt.AggregateKey == NotificationAggregateKeys.Follow(currentId)),
                It.IsAny<CancellationToken>()), Times.Once);
            _notificationServiceMock.Verify(x => x.EnqueueAggregateEventAsync(
                It.Is<NotificationAggregateEvent>(evt =>
                    evt.RecipientId == targetId &&
                    evt.Action == NotificationAggregateActionEnum.Deactivate &&
                    evt.Type == NotificationTypeEnum.FollowRequest &&
                    evt.AggregateKey == NotificationAggregateKeys.FollowRequest(currentId)),
                It.IsAny<CancellationToken>()), Times.Once);
            _realtimeServiceMock.Verify(x => x.NotifyFollowChangedAsync(
                currentId,
                targetId,
                "unfollow",
                5,
                6,
                3,
                4,
                null), Times.Once);
            _realtimeServiceMock.Verify(x => x.NotifyFollowRequestQueueChangedAsync(
                targetId,
                "remove",
                currentId,
                null), Times.Once);
            _onlinePresenceServiceMock.Verify(x => x.NotifyBlockedPairHiddenAsync(
                currentId,
                targetId,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BlockAsync_SameAccount_ThrowsBadRequestException()
        {
            // arrange
            var currentId = Guid.NewGuid();

            // act
            var act = () => _blockService.BlockAsync(currentId, currentId);

            // assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("You cannot block yourself.");
        }

        [Fact]
        public async Task BlockAsync_WhenCurrentAccountUnavailable_ThrowsForbiddenException()
        {
            // arrange
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _accountRepositoryMock
                .Setup(x => x.GetAccountById(currentId))
                .ReturnsAsync((Account?)null);

            // act
            var act = () => _blockService.BlockAsync(currentId, targetId);

            // assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You must reactivate your account to manage blocked users.");
        }

        [Fact]
        public async Task GetBlockedAccountsAsync_ReturnsMappedPagedResponse()
        {
            // arrange
            var currentId = Guid.NewGuid();
            var request = new BlockedAccountListRequest
            {
                Keyword = "blocked",
                Page = 2,
                PageSize = 10
            };
            var blockedAt = DateTime.UtcNow;

            _accountBlockRepositoryMock
                .Setup(x => x.GetBlockedAccountsAsync(
                    currentId,
                    request.Keyword,
                    request.Page,
                    request.PageSize,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((
                    new List<BlockedAccountListItemModel>
                    {
                        new()
                        {
                            AccountId = Guid.NewGuid(),
                            Username = "blocked-user",
                            FullName = "Blocked User",
                            AvatarUrl = "avatar.png",
                            BlockedAt = blockedAt
                        }
                    },
                    11));

            // act
            var result = await _blockService.GetBlockedAccountsAsync(currentId, request);

            // assert
            result.Page.Should().Be(2);
            result.PageSize.Should().Be(10);
            result.TotalItems.Should().Be(11);
            result.Items.Should().ContainSingle();
            result.Items.First().Username.Should().Be("blocked-user");
            result.Items.First().FullName.Should().Be("Blocked User");
            result.Items.First().AvatarUrl.Should().Be("avatar.png");
            result.Items.First().BlockedAt.Should().Be(blockedAt);
        }

        [Fact]
        public async Task UnblockAsync_ValidRequest_RemovesRelationAndReturnsClearedStatus()
        {
            // arrange
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var isBlockedByCurrentUser = true;

            _accountBlockRepositoryMock
                .Setup(x => x.RemoveAsync(currentId, targetId))
                .Callback(() => isBlockedByCurrentUser = false)
                .ReturnsAsync(1);

            _accountBlockRepositoryMock
                .Setup(x => x.GetRelationsAsync(currentId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                    isBlockedByCurrentUser
                        ? new List<AccountBlockRelationModel>
                        {
                            new()
                            {
                                TargetId = targetId,
                                IsBlockedByCurrentUser = true,
                                IsBlockedByTargetUser = false
                            }
                        }
                        : new List<AccountBlockRelationModel>());

            // act
            var result = await _blockService.UnblockAsync(currentId, targetId);

            // assert
            result.TargetId.Should().Be(targetId);
            result.IsBlockedByCurrentUser.Should().BeFalse();
            result.IsBlockedByTargetUser.Should().BeFalse();
            result.IsBlockedEitherWay.Should().BeFalse();

            _accountBlockRepositoryMock.Verify(x => x.RemoveAsync(currentId, targetId), Times.Once);
        }
    }
}
