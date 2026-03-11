using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Repositories.Accounts;

namespace CloudM.Tests.Repositories
{
    public class AccountRepositoryTests
    {
        [Fact]
        public async Task SearchAccountsForGroupInviteAsync_WithEmptyKeyword_KeepsCandidatesBlockedOnlyByExcludedMembers()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"account-repo-invite-search-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var now = new DateTime(2026, 3, 11, 8, 0, 0, DateTimeKind.Utc);
            var currentId = Guid.NewGuid();
            var selectedMemberId = Guid.NewGuid();
            var blockedCandidateId = Guid.NewGuid();
            var visibleCandidateId = Guid.NewGuid();

            context.Accounts.AddRange(
                BuildAccount(currentId, "current-user"),
                BuildAccount(selectedMemberId, "selected-user"),
                BuildAccount(blockedCandidateId, "blocked-candidate"),
                BuildAccount(visibleCandidateId, "visible-candidate"));

            var blockedConversationId = Guid.NewGuid();
            var visibleConversationId = Guid.NewGuid();
            context.Conversations.AddRange(
                new Conversation
                {
                    ConversationId = blockedConversationId,
                    IsGroup = false,
                    CreatedBy = currentId,
                    CreatedAt = now.AddMinutes(-30)
                },
                new Conversation
                {
                    ConversationId = visibleConversationId,
                    IsGroup = false,
                    CreatedBy = currentId,
                    CreatedAt = now.AddMinutes(-20)
                });

            context.ConversationMembers.AddRange(
                new ConversationMember
                {
                    ConversationId = blockedConversationId,
                    AccountId = currentId,
                    HasLeft = false,
                    JoinedAt = now.AddMinutes(-30)
                },
                new ConversationMember
                {
                    ConversationId = blockedConversationId,
                    AccountId = blockedCandidateId,
                    HasLeft = false,
                    JoinedAt = now.AddMinutes(-29)
                },
                new ConversationMember
                {
                    ConversationId = visibleConversationId,
                    AccountId = currentId,
                    HasLeft = false,
                    JoinedAt = now.AddMinutes(-20)
                },
                new ConversationMember
                {
                    ConversationId = visibleConversationId,
                    AccountId = visibleCandidateId,
                    HasLeft = false,
                    JoinedAt = now.AddMinutes(-19)
                });

            context.Messages.AddRange(
                new Message
                {
                    MessageId = Guid.NewGuid(),
                    ConversationId = blockedConversationId,
                    AccountId = blockedCandidateId,
                    Content = "blocked",
                    MessageType = MessageTypeEnum.Text,
                    SentAt = now.AddMinutes(-10)
                },
                new Message
                {
                    MessageId = Guid.NewGuid(),
                    ConversationId = visibleConversationId,
                    AccountId = visibleCandidateId,
                    Content = "visible",
                    MessageType = MessageTypeEnum.Text,
                    SentAt = now.AddMinutes(-5)
                });

            context.AccountBlocks.Add(new AccountBlock
            {
                BlockerId = selectedMemberId,
                BlockedId = blockedCandidateId,
                CreatedAt = now.AddMinutes(-1)
            });

            await context.SaveChangesAsync();

            var repository = new AccountRepository(context);

            var result = await repository.SearchAccountsForGroupInviteAsync(
                currentId,
                string.Empty,
                new[] { selectedMemberId },
                10);

            result.Select(x => x.AccountId)
                .Should()
                .BeEquivalentTo(new[] { visibleCandidateId, blockedCandidateId });
        }

        [Fact]
        public async Task GetFollowSuggestionsAsync_ExcludesBlockedFollowedAndPendingAccounts()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"account-repo-follow-suggestions-filter-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var now = new DateTime(2026, 3, 12, 8, 0, 0, DateTimeKind.Utc);
            var currentId = Guid.NewGuid();
            var visibleCandidateId = Guid.NewGuid();
            var followedCandidateId = Guid.NewGuid();
            var pendingCandidateId = Guid.NewGuid();
            var blockedCandidateId = Guid.NewGuid();

            context.Accounts.AddRange(
                BuildAccount(currentId, "current-user", now.AddMinutes(-10)),
                BuildAccount(visibleCandidateId, "visible-candidate", now.AddMinutes(-9)),
                BuildAccount(followedCandidateId, "followed-candidate", now.AddMinutes(-8)),
                BuildAccount(pendingCandidateId, "pending-candidate", now.AddMinutes(-7)),
                BuildAccount(blockedCandidateId, "blocked-candidate", now.AddMinutes(-6)));

            context.Follows.Add(new Follow
            {
                FollowerId = currentId,
                FollowedId = followedCandidateId,
                CreatedAt = now.AddMinutes(-5)
            });

            context.FollowRequests.Add(new FollowRequest
            {
                RequesterId = currentId,
                TargetId = pendingCandidateId,
                CreatedAt = now.AddMinutes(-4)
            });

            context.AccountBlocks.Add(new AccountBlock
            {
                BlockerId = currentId,
                BlockedId = blockedCandidateId,
                CreatedAt = now.AddMinutes(-3)
            });

            await context.SaveChangesAsync();

            var repository = new AccountRepository(context);

            var result = await repository.GetFollowSuggestionsAsync(currentId, 1, 10, false);

            result.TotalItems.Should().Be(1);
            result.Items.Select(x => x.AccountId).Should().Equal(visibleCandidateId);
        }

        [Fact]
        public async Task GetFollowSuggestionsAsync_PageSurface_PrioritizesContactThenFollowerThenMutual()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"account-repo-follow-suggestions-ranking-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var now = new DateTime(2026, 3, 12, 10, 0, 0, DateTimeKind.Utc);
            var currentId = Guid.NewGuid();
            var contactCandidateId = Guid.NewGuid();
            var followerCandidateId = Guid.NewGuid();
            var mutualCandidateId = Guid.NewGuid();
            var mutualSourceId = Guid.NewGuid();

            context.Accounts.AddRange(
                BuildAccount(currentId, "current-user", now.AddMinutes(-20)),
                BuildAccount(contactCandidateId, "contact-candidate", now.AddMinutes(-19)),
                BuildAccount(followerCandidateId, "follower-candidate", now.AddMinutes(-18)),
                BuildAccount(mutualCandidateId, "mutual-candidate", now.AddMinutes(-17)),
                BuildAccount(mutualSourceId, "mutual-source", now.AddMinutes(-16)));

            var conversationId = Guid.NewGuid();
            context.Conversations.Add(new Conversation
            {
                ConversationId = conversationId,
                IsGroup = false,
                CreatedBy = currentId,
                CreatedAt = now.AddMinutes(-15)
            });

            context.ConversationMembers.AddRange(
                new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    HasLeft = false,
                    JoinedAt = now.AddMinutes(-15)
                },
                new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = contactCandidateId,
                    HasLeft = false,
                    JoinedAt = now.AddMinutes(-14)
                });

            context.Follows.AddRange(
                new Follow
                {
                    FollowerId = followerCandidateId,
                    FollowedId = currentId,
                    CreatedAt = now.AddMinutes(-13)
                },
                new Follow
                {
                    FollowerId = currentId,
                    FollowedId = mutualSourceId,
                    CreatedAt = now.AddMinutes(-12)
                },
                new Follow
                {
                    FollowerId = mutualSourceId,
                    FollowedId = mutualCandidateId,
                    CreatedAt = now.AddMinutes(-11)
                });

            await context.SaveChangesAsync();

            var repository = new AccountRepository(context);

            var result = await repository.GetFollowSuggestionsAsync(currentId, 1, 10, false);

            result.Items.Select(x => x.AccountId).Should().ContainInOrder(
                contactCandidateId,
                followerCandidateId,
                mutualCandidateId);
        }

        private static Account BuildAccount(Guid accountId, string username, DateTime? createdAt = null)
        {
            return new Account
            {
                AccountId = accountId,
                Username = username,
                Email = $"{username}@test.local",
                FullName = username,
                PasswordHash = "hash",
                Status = AccountStatusEnum.Active,
                RoleId = (int)RoleEnum.User,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };
        }
    }
}
