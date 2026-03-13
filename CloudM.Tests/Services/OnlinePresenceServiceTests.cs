using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using CloudM.API.Hubs;
using CloudM.API.Services;
using CloudM.Application.Services.PresenceServices;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Infrastructure.Repositories.Presences;
using Microsoft.AspNetCore.SignalR;

namespace CloudM.Tests.Services
{
    public class OnlinePresenceServiceTests
    {
        [Fact]
        public async Task GetSnapshotAsync_RedisFails_ReturnsHiddenOfflineStates()
        {
            var viewerAccountId = Guid.NewGuid();
            var targetAccountId = Guid.NewGuid();

            var redisMock = new Mock<IConnectionMultiplexer>();
            var databaseMock = new Mock<IDatabase>();
            var accountBlockRepositoryMock = new Mock<IAccountBlockRepository>();
            var onlinePresenceRepositoryMock = new Mock<IOnlinePresenceRepository>();
            var userHubContextMock = new Mock<IHubContext<UserHub>>();
            var loggerMock = new Mock<ILogger<OnlinePresenceService>>();

            redisMock
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(databaseMock.Object);
            databaseMock
                .Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));

            var service = new OnlinePresenceService(
                redisMock.Object,
                accountBlockRepositoryMock.Object,
                onlinePresenceRepositoryMock.Object,
                userHubContextMock.Object,
                new MemoryPresenceSnapshotRateLimiter(
                    Options.Create(new OnlinePresenceOptions())),
                new MemoryPresenceHiddenBroadcastTracker(),
                Options.Create(new OnlinePresenceOptions()),
                new ConfigurationBuilder().AddInMemoryCollection().Build(),
                loggerMock.Object);

            var result = await service.GetSnapshotAsync(
                viewerAccountId,
                new List<Guid> { targetAccountId },
                DateTime.UtcNow);

            result.Items.Should().HaveCount(1);
            result.Items[0].AccountId.Should().Be(targetAccountId);
            result.Items[0].CanShowStatus.Should().BeFalse();
            result.Items[0].IsOnline.Should().BeFalse();
            result.Items[0].LastOnlineAt.Should().BeNull();

            onlinePresenceRepositoryMock.Verify(
                x => x.GetSnapshotAccountStatesAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            onlinePresenceRepositoryMock.Verify(
                x => x.GetContactTargetIdsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            accountBlockRepositoryMock.Verify(
                x => x.GetRelationsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task TryConsumeSnapshotRateLimitAsync_RedisFails_FallsBackToMemoryWindow()
        {
            var viewerAccountId = Guid.NewGuid();
            var redisMock = new Mock<IConnectionMultiplexer>();
            var databaseMock = new Mock<IDatabase>();
            var accountBlockRepositoryMock = new Mock<IAccountBlockRepository>();
            var onlinePresenceRepositoryMock = new Mock<IOnlinePresenceRepository>();
            var userHubContextMock = new Mock<IHubContext<UserHub>>();
            var loggerMock = new Mock<ILogger<OnlinePresenceService>>();

            redisMock
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(databaseMock.Object);
            databaseMock
                .Setup(x => x.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));

            var options = Options.Create(new OnlinePresenceOptions
            {
                SnapshotRateLimitWindowSeconds = 30,
                SnapshotRateLimitMaxRequests = 1
            });

            var service = new OnlinePresenceService(
                redisMock.Object,
                accountBlockRepositoryMock.Object,
                onlinePresenceRepositoryMock.Object,
                userHubContextMock.Object,
                new MemoryPresenceSnapshotRateLimiter(
                    options),
                new MemoryPresenceHiddenBroadcastTracker(),
                options,
                new ConfigurationBuilder().AddInMemoryCollection().Build(),
                loggerMock.Object);

            var nowUtc = DateTime.UtcNow;

            var firstResult = await service.TryConsumeSnapshotRateLimitAsync(viewerAccountId, nowUtc);
            var secondResult = await service.TryConsumeSnapshotRateLimitAsync(viewerAccountId, nowUtc);

            firstResult.Allowed.Should().BeTrue();
            firstResult.RetryAfterSeconds.Should().Be(0);
            secondResult.Allowed.Should().BeFalse();
            secondResult.RetryAfterSeconds.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task TryConsumeSnapshotRateLimitAsync_RedisTimeout_FallsBackToMemoryWindow()
        {
            var viewerAccountId = Guid.NewGuid();
            var redisMock = new Mock<IConnectionMultiplexer>();
            var databaseMock = new Mock<IDatabase>();
            var accountBlockRepositoryMock = new Mock<IAccountBlockRepository>();
            var onlinePresenceRepositoryMock = new Mock<IOnlinePresenceRepository>();
            var userHubContextMock = new Mock<IHubContext<UserHub>>();
            var loggerMock = new Mock<ILogger<OnlinePresenceService>>();

            redisMock
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(databaseMock.Object);
            databaseMock
                .Setup(x => x.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisTimeoutException("redis timeout", CommandStatus.Unknown));

            var options = Options.Create(new OnlinePresenceOptions
            {
                SnapshotRateLimitWindowSeconds = 30,
                SnapshotRateLimitMaxRequests = 1
            });

            var service = new OnlinePresenceService(
                redisMock.Object,
                accountBlockRepositoryMock.Object,
                onlinePresenceRepositoryMock.Object,
                userHubContextMock.Object,
                new MemoryPresenceSnapshotRateLimiter(
                    options),
                new MemoryPresenceHiddenBroadcastTracker(),
                options,
                new ConfigurationBuilder().AddInMemoryCollection().Build(),
                loggerMock.Object);

            var nowUtc = DateTime.UtcNow;

            var firstResult = await service.TryConsumeSnapshotRateLimitAsync(viewerAccountId, nowUtc);
            var secondResult = await service.TryConsumeSnapshotRateLimitAsync(viewerAccountId, nowUtc);

            firstResult.Allowed.Should().BeTrue();
            firstResult.RetryAfterSeconds.Should().Be(0);
            secondResult.Allowed.Should().BeFalse();
            secondResult.RetryAfterSeconds.Should().BeGreaterThan(0);
        }
    }
}
