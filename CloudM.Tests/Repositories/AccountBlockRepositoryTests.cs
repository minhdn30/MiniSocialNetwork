using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Tests.Helpers;

namespace CloudM.Tests.Repositories
{
    public class AccountBlockRepositoryTests
    {
        [Fact]
        public async Task GetRelationPairsAsync_WhenBlockedBothWays_PreservesPerViewerRelationFlags()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"account-block-repo-pairs-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var currentA = Guid.NewGuid();
            var currentB = Guid.NewGuid();
            var targetA = Guid.NewGuid();
            var targetB = Guid.NewGuid();

            context.AccountBlocks.AddRange(
                new AccountBlock
                {
                    BlockerId = currentA,
                    BlockedId = targetA
                },
                new AccountBlock
                {
                    BlockerId = targetB,
                    BlockedId = currentA
                },
                new AccountBlock
                {
                    BlockerId = currentB,
                    BlockedId = targetB
                },
                new AccountBlock
                {
                    BlockerId = targetB,
                    BlockedId = currentB
                });
            await context.SaveChangesAsync();

            var repository = new AccountBlockRepository(context);

            var result = await repository.GetRelationPairsAsync(
                new[] { currentA, currentB },
                new[] { targetA, targetB });

            result.Should().ContainEquivalentOf(new
            {
                CurrentId = currentA,
                TargetId = targetA,
                IsBlockedByCurrentUser = true,
                IsBlockedByTargetUser = false
            });
            result.Should().ContainEquivalentOf(new
            {
                CurrentId = currentA,
                TargetId = targetB,
                IsBlockedByCurrentUser = false,
                IsBlockedByTargetUser = true
            });
            result.Should().ContainEquivalentOf(new
            {
                CurrentId = currentB,
                TargetId = targetB,
                IsBlockedByCurrentUser = true,
                IsBlockedByTargetUser = true
            });
        }

        [Fact]
        public async Task GetRelationPairsAsync_WhenCurrentAndTargetSetsOverlap_ReturnsRelationForBlockedViewer()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"account-block-repo-overlap-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var viewerId = Guid.NewGuid();
            var blockedMemberId = Guid.NewGuid();

            context.AccountBlocks.Add(new AccountBlock
            {
                BlockerId = blockedMemberId,
                BlockedId = viewerId
            });
            await context.SaveChangesAsync();

            var repository = new AccountBlockRepository(context);

            var result = await repository.GetRelationPairsAsync(
                new[] { viewerId, blockedMemberId },
                new[] { viewerId, blockedMemberId });

            result.Should().ContainEquivalentOf(new
            {
                CurrentId = viewerId,
                TargetId = blockedMemberId,
                IsBlockedByCurrentUser = false,
                IsBlockedByTargetUser = true
            });
            result.Should().ContainEquivalentOf(new
            {
                CurrentId = blockedMemberId,
                TargetId = viewerId,
                IsBlockedByCurrentUser = true,
                IsBlockedByTargetUser = false
            });
        }

        [Fact]
        public async Task GetBlockedAccountsAsync_WhenBlockedAccountIsInactive_StillReturnsEntry()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"account-block-repo-blocked-list-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var blocker = TestDataFactory.CreateAccount(username: "blocker");
            var blocked = TestDataFactory.CreateAccount(
                username: "inactive-user",
                status: AccountStatusEnum.Inactive);

            context.Accounts.AddRange(blocker, blocked);
            context.AccountBlocks.Add(new AccountBlock
            {
                BlockerId = blocker.AccountId,
                BlockedId = blocked.AccountId,
                BlockedSnapshotUsername = blocked.Username
            });
            await context.SaveChangesAsync();

            var repository = new AccountBlockRepository(context);

            var (items, totalItems) = await repository.GetBlockedAccountsAsync(
                blocker.AccountId,
                null,
                1,
                20);

            totalItems.Should().Be(1);
            items.Should().ContainSingle();
            items[0].AccountId.Should().Be(blocked.AccountId);
            items[0].Username.Should().Be("inactive-user");
        }
    }
}
