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

        private static Account BuildAccount(Guid accountId, string username)
        {
            return new Account
            {
                AccountId = accountId,
                Username = username,
                Email = $"{username}@test.local",
                FullName = username,
                PasswordHash = "hash",
                Status = AccountStatusEnum.Active,
                RoleId = (int)RoleEnum.User
            };
        }
    }
}
