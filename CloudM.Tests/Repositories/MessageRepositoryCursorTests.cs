using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Repositories.Messages;

namespace CloudM.Tests.Repositories
{
    public class MessageRepositoryCursorTests
    {
        [Fact]
        public async Task GetMessagesByConversationId_InitialAndOlderCursor_ShouldReturnStableDescendingPages()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"msg-cursor-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var repo = new MessageRepository(context);

            var now = new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc);
            var conversationId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            await SeedConversationWithMembersAsync(context, conversationId, currentId, otherId, now);

            var m1 = Guid.NewGuid();
            var m2 = Guid.NewGuid();
            var m3 = Guid.NewGuid();
            var m4 = Guid.NewGuid();
            var m5 = Guid.NewGuid();

            context.Messages.AddRange(
                CreateTextMessage(m1, conversationId, otherId, "m1", now.AddMinutes(1)),
                CreateTextMessage(m2, conversationId, otherId, "m2", now.AddMinutes(2)),
                CreateTextMessage(m3, conversationId, otherId, "m3", now.AddMinutes(3)),
                CreateTextMessage(m4, conversationId, otherId, "m4", now.AddMinutes(4)),
                CreateTextMessage(m5, conversationId, otherId, "m5", now.AddMinutes(5)));
            await context.SaveChangesAsync();

            var first = await repo.GetMessagesByConversationId(conversationId, currentId, null, 2);
            first.Items.Select(x => x.MessageId).Should().Equal(m5, m4);
            first.HasMoreOlder.Should().BeTrue();
            first.HasMoreNewer.Should().BeFalse();
            first.OlderCursor.Should().NotBeNullOrWhiteSpace();
            first.NewerCursor.Should().BeNull();

            var second = await repo.GetMessagesByConversationId(conversationId, currentId, first.OlderCursor, 2);
            second.Items.Select(x => x.MessageId).Should().Equal(m3, m2);
            second.HasMoreOlder.Should().BeTrue();
            second.HasMoreNewer.Should().BeTrue();
            second.OlderCursor.Should().NotBeNullOrWhiteSpace();
            second.NewerCursor.Should().NotBeNullOrWhiteSpace();

            var reloadNewer = await repo.GetMessagesByConversationId(conversationId, currentId, second.NewerCursor, 2);
            reloadNewer.Items.Select(x => x.MessageId).Should().Equal(m5, m4);
            reloadNewer.HasMoreOlder.Should().BeTrue();
            reloadNewer.HasMoreNewer.Should().BeFalse();
        }

        [Fact]
        public async Task GetMessagesByConversationId_ContextCursor_ShouldIncludeAnchorAndOlderMessages()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"msg-context-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var repo = new MessageRepository(context);

            var now = new DateTime(2026, 3, 3, 11, 0, 0, DateTimeKind.Utc);
            var conversationId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            await SeedConversationWithMembersAsync(context, conversationId, currentId, otherId, now);

            var m1 = Guid.NewGuid();
            var m2 = Guid.NewGuid();
            var m3 = Guid.NewGuid();
            var m4 = Guid.NewGuid();
            var m5 = Guid.NewGuid();
            var m3SentAt = now.AddMinutes(3);

            context.Messages.AddRange(
                CreateTextMessage(m1, conversationId, otherId, "m1", now.AddMinutes(1)),
                CreateTextMessage(m2, conversationId, otherId, "m2", now.AddMinutes(2)),
                CreateTextMessage(m3, conversationId, otherId, "m3", m3SentAt),
                CreateTextMessage(m4, conversationId, otherId, "m4", now.AddMinutes(4)),
                CreateTextMessage(m5, conversationId, otherId, "m5", now.AddMinutes(5)));
            await context.SaveChangesAsync();

            var contextCursor = $"c|{m3SentAt.Ticks}|{m3:N}";
            var page = await repo.GetMessagesByConversationId(conversationId, currentId, contextCursor, 3);

            page.Items.Select(x => x.MessageId).Should().Equal(m3, m2, m1);
            page.HasMoreOlder.Should().BeFalse();
            page.HasMoreNewer.Should().BeTrue();
            page.OlderCursor.Should().BeNull();
            page.NewerCursor.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task GetMessagesByConversationId_WithSameSentAt_ShouldUseMessageIdTieBreaker()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"msg-tiebreaker-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var repo = new MessageRepository(context);

            var now = new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc);
            var conversationId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            await SeedConversationWithMembersAsync(context, conversationId, currentId, otherId, now);

            var idLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var idMid = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var idHigh = Guid.Parse("00000000-0000-0000-0000-000000000003");
            var idOlder = Guid.Parse("00000000-0000-0000-0000-000000000004");
            var sameSentAt = now.AddMinutes(10);

            context.Messages.AddRange(
                CreateTextMessage(idLow, conversationId, otherId, "low", sameSentAt),
                CreateTextMessage(idMid, conversationId, otherId, "mid", sameSentAt),
                CreateTextMessage(idHigh, conversationId, otherId, "high", sameSentAt),
                CreateTextMessage(idOlder, conversationId, otherId, "older", now.AddMinutes(9)));
            await context.SaveChangesAsync();

            var first = await repo.GetMessagesByConversationId(conversationId, currentId, null, 2);
            first.Items.Select(x => x.MessageId).Should().Equal(idHigh, idMid);

            var second = await repo.GetMessagesByConversationId(conversationId, currentId, first.OlderCursor, 2);
            second.Items.Select(x => x.MessageId).Should().Equal(idLow, idOlder);
        }

        private static async Task SeedConversationWithMembersAsync(
            AppDbContext context,
            Guid conversationId,
            Guid currentId,
            Guid otherId,
            DateTime now)
        {
            var current = new Account
            {
                AccountId = currentId,
                Username = "current-user",
                Email = $"current-{Guid.NewGuid():N}@test.local",
                FullName = "Current User",
                PasswordHash = "hash",
                Status = AccountStatusEnum.Active,
                RoleId = 2
            };
            var other = new Account
            {
                AccountId = otherId,
                Username = "other-user",
                Email = $"other-{Guid.NewGuid():N}@test.local",
                FullName = "Other User",
                PasswordHash = "hash",
                Status = AccountStatusEnum.Active,
                RoleId = 2
            };

            context.Accounts.AddRange(current, other);
            context.Conversations.Add(new Conversation
            {
                ConversationId = conversationId,
                IsGroup = false,
                CreatedAt = now.AddHours(-1),
                CreatedBy = currentId
            });
            context.ConversationMembers.AddRange(
                new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    JoinedAt = now.AddHours(-1)
                },
                new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = otherId,
                    JoinedAt = now.AddHours(-1)
                });

            await context.SaveChangesAsync();
        }

        private static Message CreateTextMessage(
            Guid messageId,
            Guid conversationId,
            Guid senderId,
            string content,
            DateTime sentAt)
        {
            return new Message
            {
                MessageId = messageId,
                ConversationId = conversationId,
                AccountId = senderId,
                Content = content,
                MessageType = MessageTypeEnum.Text,
                SentAt = sentAt
            };
        }
    }
}
