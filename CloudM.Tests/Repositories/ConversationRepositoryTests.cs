using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Repositories.Conversations;

namespace CloudM.Tests.Repositories
{
    public class ConversationRepositoryTests
    {
        [Fact]
        public async Task GetConversationsByCursorAsync_ShouldKeepContractParity_ForMixedConversationData()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"conv-repo-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var repo = new ConversationRepository(context);

            var now = new DateTime(2026, 2, 12, 10, 0, 0, DateTimeKind.Utc);
            var currentId = Guid.NewGuid();
            var otherPrivateId = Guid.NewGuid();
            var otherGroupId = Guid.NewGuid();

            var current = new Account
            {
                AccountId = currentId,
                Username = "current",
                Email = "current@test.local",
                FullName = "Current User",
                PasswordHash = "hash",
                Status = AccountStatusEnum.Active,
                RoleId = 2
            };
            var otherPrivate = new Account
            {
                AccountId = otherPrivateId,
                Username = "private-user",
                Email = "private@test.local",
                FullName = "Private User",
                PasswordHash = "hash",
                Status = AccountStatusEnum.Active,
                RoleId = 2,
                AvatarUrl = "https://cdn/private.png"
            };
            var otherGroup = new Account
            {
                AccountId = otherGroupId,
                Username = "group-user",
                Email = "group@test.local",
                FullName = "Group User",
                PasswordHash = "hash",
                Status = AccountStatusEnum.Active,
                RoleId = 2,
                AvatarUrl = "https://cdn/group.png"
            };
            context.Accounts.AddRange(current, otherPrivate, otherGroup);

            var privateConvId = Guid.NewGuid();
            var groupConvId = Guid.NewGuid();
            context.Conversations.AddRange(
                new Conversation
                {
                    ConversationId = privateConvId,
                    IsGroup = false,
                    CreatedAt = now.AddDays(-1),
                    CreatedBy = currentId
                },
                new Conversation
                {
                    ConversationId = groupConvId,
                    IsGroup = true,
                    ConversationName = "QA Group",
                    ConversationAvatar = "https://cdn/group-avatar.png",
                    CreatedAt = now.AddDays(-2),
                    CreatedBy = currentId
                });

            context.ConversationMembers.AddRange(
                new ConversationMember
                {
                    ConversationId = privateConvId,
                    AccountId = currentId,
                    LastSeenAt = now.AddMinutes(-5)
                },
                new ConversationMember
                {
                    ConversationId = privateConvId,
                    AccountId = otherPrivateId,
                    Nickname = "Buddy",
                    LastSeenAt = now.AddMinutes(-2)
                },
                new ConversationMember
                {
                    ConversationId = groupConvId,
                    AccountId = currentId,
                    LastSeenAt = now.AddMinutes(-5)
                },
                new ConversationMember
                {
                    ConversationId = groupConvId,
                    AccountId = otherPrivateId,
                    Nickname = "Alice",
                    LastSeenAt = now.AddMinutes(-4)
                },
                new ConversationMember
                {
                    ConversationId = groupConvId,
                    AccountId = otherGroupId,
                    Nickname = "Bob",
                    LastSeenAt = now.AddMinutes(-3)
                });

            var privateOldMsgId = Guid.NewGuid();
            var privateLatestMsgId = Guid.NewGuid();
            var groupOldMsgId = Guid.NewGuid();
            var groupVisibleLatestMsgId = Guid.NewGuid();
            var groupHiddenLatestMsgId = Guid.NewGuid();

            context.Messages.AddRange(
                new Message
                {
                    MessageId = privateOldMsgId,
                    ConversationId = privateConvId,
                    AccountId = otherPrivateId,
                    Content = "older private",
                    MessageType = MessageTypeEnum.Text,
                    SentAt = now.AddMinutes(-6)
                },
                new Message
                {
                    MessageId = privateLatestMsgId,
                    ConversationId = privateConvId,
                    AccountId = currentId,
                    Content = null,
                    MessageType = MessageTypeEnum.Media,
                    SentAt = now.AddMinutes(-4)
                },
                new Message
                {
                    MessageId = groupOldMsgId,
                    ConversationId = groupConvId,
                    AccountId = otherPrivateId,
                    Content = "group old",
                    MessageType = MessageTypeEnum.Text,
                    SentAt = now.AddMinutes(-8)
                },
                new Message
                {
                    MessageId = groupVisibleLatestMsgId,
                    ConversationId = groupConvId,
                    AccountId = otherGroupId,
                    Content = "group visible latest",
                    MessageType = MessageTypeEnum.Text,
                    SentAt = now.AddMinutes(-3)
                },
                new Message
                {
                    MessageId = groupHiddenLatestMsgId,
                    ConversationId = groupConvId,
                    AccountId = otherGroupId,
                    Content = "group hidden latest",
                    MessageType = MessageTypeEnum.Text,
                    SentAt = now.AddMinutes(-1)
                });

            context.MessageMedias.Add(new MessageMedia
            {
                MessageMediaId = Guid.NewGuid(),
                MessageId = privateLatestMsgId,
                MediaUrl = "https://cdn/private-latest.jpg",
                MediaType = MediaTypeEnum.Image
            });

            context.MessageHiddens.Add(new MessageHidden
            {
                MessageId = groupHiddenLatestMsgId,
                AccountId = currentId,
                HiddenAt = now
            });

            await context.SaveChangesAsync();

            var (items, hasMore) = await repo.GetConversationsByCursorAsync(currentId, null, null, null, null, 10);

            items.Should().HaveCount(2);
            hasMore.Should().BeFalse();

            var groupItem = items.First(x => x.ConversationId == groupConvId);
            var privateItem = items.First(x => x.ConversationId == privateConvId);

            items[0].ConversationId.Should().Be(groupConvId);
            groupItem.UnreadCount.Should().Be(1);
            groupItem.LastMessage.Should().NotBeNull();
            groupItem.LastMessage!.MessageId.Should().Be(groupVisibleLatestMsgId);
            groupItem.LastMessageSentAt.Should().Be(now.AddMinutes(-3));
            groupItem.OtherMember.Should().BeNull();
            groupItem.LastMessageSeenCount.Should().Be(0);
            groupItem.LastMessageSeenBy.Should().BeNull();

            privateItem.UnreadCount.Should().Be(0);
            privateItem.LastMessage.Should().NotBeNull();
            privateItem.LastMessage!.MessageId.Should().Be(privateLatestMsgId);
            privateItem.LastMessage.Medias.Should().NotBeNull();
            privateItem.LastMessage.Medias.Should().HaveCount(1);
            privateItem.LastMessageSentAt.Should().Be(now.AddMinutes(-4));
            privateItem.OtherMember.Should().NotBeNull();
            privateItem.OtherMember!.AccountId.Should().Be(otherPrivateId);
            privateItem.LastMessageSeenCount.Should().Be(1);
            privateItem.LastMessageSeenBy.Should().NotBeNull();
            privateItem.LastMessageSeenBy!.Should().ContainSingle(x => x.AccountId == otherPrivateId);
        }

        [Fact]
        public async Task GetConversationsByCursorAsync_ShouldIgnoreLegacySystemMessages_ForLastMessageAndUnread()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"conv-repo-social-filter-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var repo = new ConversationRepository(context);

            var now = new DateTime(2026, 3, 11, 8, 0, 0, DateTimeKind.Utc);
            var currentId = Guid.NewGuid();
            var socialMemberId = Guid.NewGuid();
            var legacyAdminId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            context.Accounts.AddRange(
                new Account
                {
                    AccountId = currentId,
                    Username = "current",
                    Email = "current@test.local",
                    FullName = "Current User",
                    PasswordHash = "hash",
                    Status = AccountStatusEnum.Active,
                    RoleId = (int)RoleEnum.User
                },
                new Account
                {
                    AccountId = socialMemberId,
                    Username = "social-user",
                    Email = "social@test.local",
                    FullName = "Social User",
                    PasswordHash = "hash",
                    Status = AccountStatusEnum.Active,
                    RoleId = (int)RoleEnum.User
                },
                new Account
                {
                    AccountId = legacyAdminId,
                    Username = "legacy-admin",
                    Email = "admin@test.local",
                    FullName = "Legacy Admin",
                    PasswordHash = "hash",
                    Status = AccountStatusEnum.Active,
                    RoleId = (int)RoleEnum.Admin
                });

            context.Conversations.Add(new Conversation
            {
                ConversationId = conversationId,
                IsGroup = true,
                ConversationName = "Legacy Group",
                CreatedAt = now.AddDays(-1),
                CreatedBy = currentId,
                Owner = currentId
            });

            context.ConversationMembers.AddRange(
                new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    LastSeenAt = now.AddMinutes(-10)
                },
                new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = socialMemberId,
                    LastSeenAt = now.AddMinutes(-5)
                },
                new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = legacyAdminId,
                    LastSeenAt = now.AddMinutes(-1)
                });

            var socialMessageId = Guid.NewGuid();
            var legacyMessageId = Guid.NewGuid();

            context.Messages.AddRange(
                new Message
                {
                    MessageId = socialMessageId,
                    ConversationId = conversationId,
                    AccountId = socialMemberId,
                    Content = "social message",
                    MessageType = MessageTypeEnum.Text,
                    SentAt = now.AddMinutes(-3)
                },
                new Message
                {
                    MessageId = legacyMessageId,
                    ConversationId = conversationId,
                    AccountId = legacyAdminId,
                    Content = "legacy admin message",
                    MessageType = MessageTypeEnum.Text,
                    SentAt = now.AddMinutes(-1)
                });

            await context.SaveChangesAsync();

            var (items, hasMore) = await repo.GetConversationsByCursorAsync(currentId, null, null, null, null, 10);

            hasMore.Should().BeFalse();
            items.Should().ContainSingle(x => x.ConversationId == conversationId);

            var conversation = items.Single(x => x.ConversationId == conversationId);
            conversation.LastMessage.Should().NotBeNull();
            conversation.LastMessage!.MessageId.Should().Be(socialMessageId);
            conversation.LastMessage.Content.Should().Be("social message");
            conversation.LastMessageSentAt.Should().Be(now.AddMinutes(-3));
            conversation.UnreadCount.Should().Be(1);
        }

        [Fact]
        public async Task GetConversationsByCursorAsync_ShouldExcludePrivateConversation_WithLegacySystemAccount()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"conv-repo-private-legacy-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var repo = new ConversationRepository(context);

            var now = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc);
            var currentId = Guid.NewGuid();
            var legacyAdminId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            context.Accounts.AddRange(
                new Account
                {
                    AccountId = currentId,
                    Username = "current",
                    Email = "current-private@test.local",
                    FullName = "Current User",
                    PasswordHash = "hash",
                    Status = AccountStatusEnum.Active,
                    RoleId = (int)RoleEnum.User
                },
                new Account
                {
                    AccountId = legacyAdminId,
                    Username = "legacy-admin",
                    Email = "legacy-private@test.local",
                    FullName = "Legacy Admin",
                    PasswordHash = "hash",
                    Status = AccountStatusEnum.Active,
                    RoleId = (int)RoleEnum.Admin
                });

            context.Conversations.Add(new Conversation
            {
                ConversationId = conversationId,
                IsGroup = false,
                CreatedAt = now.AddDays(-1),
                CreatedBy = currentId
            });

            context.ConversationMembers.AddRange(
                new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    LastSeenAt = now.AddMinutes(-10)
                },
                new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = legacyAdminId,
                    LastSeenAt = now.AddMinutes(-9)
                });

            context.Messages.Add(new Message
            {
                MessageId = Guid.NewGuid(),
                ConversationId = conversationId,
                AccountId = currentId,
                Content = "hello legacy",
                MessageType = MessageTypeEnum.Text,
                SentAt = now.AddMinutes(-5)
            });

            await context.SaveChangesAsync();

            var (items, hasMore) = await repo.GetConversationsByCursorAsync(currentId, null, null, null, null, 10);

            hasMore.Should().BeFalse();
            items.Should().NotContain(x => x.ConversationId == conversationId);
        }

        [Fact]
        public async Task GetConversationMetaDataAsync_ShouldReturnNull_ForPrivateConversationWithLegacySystemAccount()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"conv-repo-private-meta-legacy-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var repo = new ConversationRepository(context);

            var currentId = Guid.NewGuid();
            var legacyAdminId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            context.Accounts.AddRange(
                new Account
                {
                    AccountId = currentId,
                    Username = "current",
                    Email = "current-meta@test.local",
                    FullName = "Current User",
                    PasswordHash = "hash",
                    Status = AccountStatusEnum.Active,
                    RoleId = (int)RoleEnum.User
                },
                new Account
                {
                    AccountId = legacyAdminId,
                    Username = "legacy-admin",
                    Email = "legacy-meta@test.local",
                    FullName = "Legacy Admin",
                    PasswordHash = "hash",
                    Status = AccountStatusEnum.Active,
                    RoleId = (int)RoleEnum.Admin
                });

            context.Conversations.Add(new Conversation
            {
                ConversationId = conversationId,
                IsGroup = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentId
            });

            context.ConversationMembers.AddRange(
                new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = currentId
                },
                new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = legacyAdminId
                });

            await context.SaveChangesAsync();

            var metadata = await repo.GetConversationMetaDataAsync(conversationId, currentId);

            metadata.Should().BeNull();
        }
    }
}
