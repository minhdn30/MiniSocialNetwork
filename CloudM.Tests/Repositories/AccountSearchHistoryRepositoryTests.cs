using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Repositories.AccountSearchHistories;

namespace CloudM.Tests.Repositories
{
    public class AccountSearchHistoryRepositoryTests
    {
        [Fact]
        public async Task GetSidebarSearchHistoryAsync_FiltersUnavailableTargetsAndOrdersByLastSearchedAt()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"account-search-history-repo-sidebar-history-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var currentId = Guid.NewGuid();
            var visibleRecentId = Guid.NewGuid();
            var visibleOlderId = Guid.NewGuid();
            var blockedId = Guid.NewGuid();
            var inactiveId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var now = new DateTime(2026, 3, 12, 12, 0, 0, DateTimeKind.Utc);

            context.Accounts.AddRange(
                BuildAccount(currentId, "current-user", now.AddMinutes(-10)),
                BuildAccount(visibleRecentId, "visible-recent", now.AddMinutes(-9)),
                BuildAccount(visibleOlderId, "visible-older", now.AddMinutes(-8)),
                BuildAccount(blockedId, "blocked-user", now.AddMinutes(-7)),
                BuildAccount(inactiveId, "inactive-user", now.AddMinutes(-6), AccountStatusEnum.Inactive),
                BuildAccount(adminId, "admin-user", now.AddMinutes(-5), AccountStatusEnum.Active, RoleEnum.Admin));

            context.AccountBlocks.Add(new AccountBlock
            {
                BlockerId = currentId,
                BlockedId = blockedId,
                CreatedAt = now.AddMinutes(-1)
            });

            context.AccountSearchHistories.AddRange(
                new AccountSearchHistory
                {
                    CurrentId = currentId,
                    TargetId = visibleOlderId,
                    CreatedAt = now.AddMinutes(-30),
                    LastSearchedAt = now.AddMinutes(-20)
                },
                new AccountSearchHistory
                {
                    CurrentId = currentId,
                    TargetId = visibleRecentId,
                    CreatedAt = now.AddMinutes(-25),
                    LastSearchedAt = now.AddMinutes(-5)
                },
                new AccountSearchHistory
                {
                    CurrentId = currentId,
                    TargetId = blockedId,
                    CreatedAt = now.AddMinutes(-24),
                    LastSearchedAt = now.AddMinutes(-4)
                },
                new AccountSearchHistory
                {
                    CurrentId = currentId,
                    TargetId = inactiveId,
                    CreatedAt = now.AddMinutes(-23),
                    LastSearchedAt = now.AddMinutes(-3)
                },
                new AccountSearchHistory
                {
                    CurrentId = currentId,
                    TargetId = adminId,
                    CreatedAt = now.AddMinutes(-22),
                    LastSearchedAt = now.AddMinutes(-2)
                });

            await context.SaveChangesAsync();

            var repository = new AccountSearchHistoryRepository(context);

            var result = await repository.GetSidebarSearchHistoryAsync(currentId, 10);

            result.Select(x => x.AccountId).Should().Equal(visibleRecentId, visibleOlderId);
            result[0].LastSearchedAt.Should().Be(now.AddMinutes(-5));
            result[1].LastSearchedAt.Should().Be(now.AddMinutes(-20));
        }

        [Fact]
        public async Task GetSidebarSearchHistoryAsync_WhenLimitIsProvided_ReturnsOnlyMostRecentItems()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"account-search-history-repo-limit-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var currentId = Guid.NewGuid();
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            var thirdId = Guid.NewGuid();
            var now = new DateTime(2026, 3, 12, 13, 30, 0, DateTimeKind.Utc);

            context.Accounts.AddRange(
                BuildAccount(currentId, "current-user"),
                BuildAccount(firstId, "first-user"),
                BuildAccount(secondId, "second-user"),
                BuildAccount(thirdId, "third-user"));

            context.AccountSearchHistories.AddRange(
                new AccountSearchHistory
                {
                    CurrentId = currentId,
                    TargetId = firstId,
                    CreatedAt = now.AddMinutes(-30),
                    LastSearchedAt = now.AddMinutes(-3)
                },
                new AccountSearchHistory
                {
                    CurrentId = currentId,
                    TargetId = secondId,
                    CreatedAt = now.AddMinutes(-29),
                    LastSearchedAt = now.AddMinutes(-2)
                },
                new AccountSearchHistory
                {
                    CurrentId = currentId,
                    TargetId = thirdId,
                    CreatedAt = now.AddMinutes(-28),
                    LastSearchedAt = now.AddMinutes(-1)
                });

            await context.SaveChangesAsync();

            var repository = new AccountSearchHistoryRepository(context);

            var result = await repository.GetSidebarSearchHistoryAsync(currentId, 2);

            result.Should().HaveCount(2);
            result.Select(x => x.AccountId).Should().Equal(thirdId, secondId);
        }

        [Fact]
        public async Task UpsertSidebarSearchHistoryAsync_WhenCalledTwice_UpdatesExistingRowInsteadOfDuplicating()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"account-search-history-repo-upsert-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var firstSearchedAt = new DateTime(2026, 3, 12, 13, 0, 0, DateTimeKind.Utc);
            var secondSearchedAt = firstSearchedAt.AddMinutes(10);

            context.Accounts.AddRange(
                BuildAccount(currentId, "current-user"),
                BuildAccount(targetId, "target-user"));
            await context.SaveChangesAsync();

            var repository = new AccountSearchHistoryRepository(context);

            await repository.UpsertSidebarSearchHistoryAsync(currentId, targetId, firstSearchedAt);
            await context.SaveChangesAsync();
            await repository.UpsertSidebarSearchHistoryAsync(currentId, targetId, secondSearchedAt);
            await context.SaveChangesAsync();

            context.AccountSearchHistories.Should().ContainSingle();
            context.AccountSearchHistories.Single().LastSearchedAt.Should().Be(secondSearchedAt);
            context.AccountSearchHistories.Single().CreatedAt.Should().Be(firstSearchedAt);

            await repository.DeleteSidebarSearchHistoryAsync(currentId, targetId);
            await context.SaveChangesAsync();

            context.AccountSearchHistories.Should().BeEmpty();
        }

        private static Account BuildAccount(
            Guid accountId,
            string username,
            DateTime? createdAt = null,
            AccountStatusEnum status = AccountStatusEnum.Active,
            RoleEnum role = RoleEnum.User)
        {
            return new Account
            {
                AccountId = accountId,
                Username = username,
                Email = $"{username}@test.local",
                FullName = username,
                PasswordHash = "hash",
                Status = status,
                RoleId = (int)role,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };
        }
    }
}
