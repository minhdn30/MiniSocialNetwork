using CloudM.Application.DTOs.NotificationDTOs;
using CloudM.Application.Services.NotificationServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Repositories.Notifications;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CloudM.Tests.Services
{
    public class NotificationServiceTests
    {
        [Fact]
        public async Task GetNotificationsAsync_PublicStoryTarget_ShouldRemainAvailableWithoutFollow()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var owner = CreateAccount("story-owner");
            var actor = CreateAccount("actor");
            var story = new Story
            {
                StoryId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                ContentType = StoryContentTypeEnum.Image,
                MediaUrl = "https://cdn/story.jpg",
                Privacy = StoryPrivacyEnum.Public,
                CreatedAt = now.AddMinutes(-5),
                ExpiresAt = now.AddHours(12),
                IsDeleted = false
            };

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.StoryReact,
                AggregateKey = NotificationAggregateKeys.StoryReact(story.StoryId),
                State = NotificationStateEnum.Active,
                IsRead = false,
                CreatedAt = now.AddMinutes(-4),
                LastEventAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3),
                ActorCount = 1,
                EventCount = 1,
                LastActorId = actor.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = actor.AccountId,
                    Username = actor.Username,
                    FullName = actor.FullName,
                    AvatarUrl = actor.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Story,
                TargetId = story.StoryId
            };

            var contribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.StoryReact,
                SourceId = actor.AccountId,
                ActorId = actor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3)
            };

            await context.Accounts.AddRangeAsync(recipient, owner, actor);
            await context.Stories.AddAsync(story);
            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var result = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "all" });

            var item = Assert.Single(result.Items);
            Assert.Equal((int)NotificationStateEnum.Active, item.State);
            Assert.True(item.CanOpen);
            Assert.Equal(story.StoryId, item.TargetId);
        }

        [Fact]
        public async Task GetNotificationsAsync_WhenLastActorBecomesInactive_ShouldTurnUnavailable()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var owner = CreateAccount("post-owner");
            var actor = CreateAccount("actor");
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                PostCode = "POST12345678",
                Privacy = PostPrivacyEnum.Public,
                IsDeleted = false,
                CreatedAt = now.AddMinutes(-10)
            };

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = NotificationAggregateKeys.PostReact(post.PostId),
                State = NotificationStateEnum.Active,
                IsRead = false,
                CreatedAt = now.AddMinutes(-4),
                LastEventAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3),
                ActorCount = 1,
                EventCount = 1,
                LastActorId = actor.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = actor.AccountId,
                    Username = actor.Username,
                    FullName = actor.FullName,
                    AvatarUrl = actor.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = post.PostId
            };

            var contribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.PostReact,
                SourceId = actor.AccountId,
                ActorId = actor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3)
            };

            await context.Accounts.AddRangeAsync(recipient, owner, actor);
            await context.Posts.AddAsync(post);
            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.SaveChangesAsync();

            actor.Status = AccountStatusEnum.Inactive;
            context.Accounts.Update(actor);
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var result = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "all" });

            var item = Assert.Single(result.Items);
            Assert.Equal((int)NotificationStateEnum.Unavailable, item.State);
            Assert.False(item.CanOpen);
            Assert.Null(item.Actor);
        }

        [Fact]
        public async Task GetNotificationsAsync_WhenActorProfileChanges_ShouldUseCurrentUsernameAndAvatar()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var owner = CreateAccount("owner");
            var actor = CreateAccount("actor-old");
            actor.AvatarUrl = "https://cdn.example.com/avatar-old.jpg";

            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                PostCode = "POSTLIVEACTOR",
                Privacy = PostPrivacyEnum.Public,
                IsDeleted = false,
                CreatedAt = now.AddMinutes(-10)
            };

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = NotificationAggregateKeys.PostReact(post.PostId),
                State = NotificationStateEnum.Active,
                IsRead = false,
                CreatedAt = now.AddMinutes(-4),
                LastEventAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3),
                ActorCount = 1,
                EventCount = 1,
                LastActorId = actor.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = actor.AccountId,
                    Username = actor.Username,
                    FullName = actor.FullName,
                    AvatarUrl = actor.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = post.PostId
            };

            var contribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.PostReact,
                SourceId = actor.AccountId,
                ActorId = actor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3)
            };

            await context.Accounts.AddRangeAsync(recipient, owner, actor);
            await context.Posts.AddAsync(post);
            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.SaveChangesAsync();

            actor.Username = "actor-new";
            actor.FullName = "actor new full";
            actor.AvatarUrl = "https://cdn.example.com/avatar-new.jpg";
            context.Accounts.Update(actor);
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var result = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "all" });

            var item = Assert.Single(result.Items);
            Assert.NotNull(item.Actor);
            Assert.Equal("actor-new", item.Actor!.Username);
            Assert.Equal("actor new full", item.Actor.FullName);
            Assert.Equal("https://cdn.example.com/avatar-new.jpg", item.Actor.AvatarUrl);
            Assert.Contains("actor-new", item.Text, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ProjectAsync_DeactivateWithKeepWhenEmptyFalse_ShouldRemoveNotification()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var actor = CreateAccount("actor");
            await context.Accounts.AddRangeAsync(recipient, actor);

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = NotificationAggregateKeys.PostReact(Guid.NewGuid()),
                State = NotificationStateEnum.Active,
                IsRead = false,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2),
                ActorCount = 1,
                EventCount = 1,
                LastActorId = actor.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = actor.AccountId,
                    Username = actor.Username,
                    FullName = actor.FullName,
                    AvatarUrl = actor.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = Guid.NewGuid()
            };

            var contribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.PostReact,
                SourceId = actor.AccountId,
                ActorId = actor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2)
            };

            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.SaveChangesAsync();

            var payload = new NotificationAggregateChangedPayload
            {
                Action = NotificationAggregateActionEnum.Deactivate,
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = notification.AggregateKey,
                SourceType = NotificationSourceTypeEnum.PostReact,
                SourceId = actor.AccountId,
                ActorId = actor.AccountId,
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = notification.TargetId,
                KeepWhenEmpty = false,
                OccurredAt = now
            };

            var outbox = new NotificationOutbox
            {
                OutboxId = Guid.NewGuid(),
                EventType = NotificationOutboxEventTypes.AggregateChanged,
                RecipientId = recipient.AccountId,
                PayloadJson = JsonSerializer.Serialize(payload),
                OccurredAt = now,
                Status = NotificationOutboxStatusEnum.Pending,
                NextRetryAt = now
            };

            var projector = new NotificationProjector(new NotificationRepository(context), context);
            var projectionResult = await projector.ProjectAsync(outbox);

            Assert.Equal(NotificationProjectionActionEnum.Remove, projectionResult.Action);
            Assert.Equal(0, await context.Notifications.CountAsync());
            Assert.Equal(0, await context.NotificationContributions.CountAsync());
        }

        [Fact]
        public async Task ProjectAsync_DeactivateWithKeepWhenEmptyTrue_ShouldKeepUnavailableItem()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;
            var postId = Guid.NewGuid();

            var recipient = CreateAccount("recipient");
            var actor = CreateAccount("actor");
            await context.Accounts.AddRangeAsync(recipient, actor);

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostTag,
                AggregateKey = NotificationAggregateKeys.PostTag(postId),
                State = NotificationStateEnum.Active,
                IsRead = false,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2),
                ActorCount = 1,
                EventCount = 1,
                LastActorId = actor.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = actor.AccountId,
                    Username = actor.Username,
                    FullName = actor.FullName,
                    AvatarUrl = actor.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = postId
            };

            var contribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.PostTag,
                SourceId = postId,
                ActorId = actor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2)
            };

            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.SaveChangesAsync();

            var payload = new NotificationAggregateChangedPayload
            {
                Action = NotificationAggregateActionEnum.Deactivate,
                Type = NotificationTypeEnum.PostTag,
                AggregateKey = notification.AggregateKey,
                SourceType = NotificationSourceTypeEnum.PostTag,
                SourceId = postId,
                ActorId = actor.AccountId,
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = postId,
                KeepWhenEmpty = true,
                OccurredAt = now
            };

            var outbox = new NotificationOutbox
            {
                OutboxId = Guid.NewGuid(),
                EventType = NotificationOutboxEventTypes.AggregateChanged,
                RecipientId = recipient.AccountId,
                PayloadJson = JsonSerializer.Serialize(payload),
                OccurredAt = now,
                Status = NotificationOutboxStatusEnum.Pending,
                NextRetryAt = now
            };

            var projector = new NotificationProjector(new NotificationRepository(context), context);
            var projectionResult = await projector.ProjectAsync(outbox);

            Assert.Equal(NotificationProjectionActionEnum.Upsert, projectionResult.Action);
            var reloaded = await context.Notifications
                .AsNoTracking()
                .SingleAsync(x => x.NotificationId == notification.NotificationId);
            Assert.Equal(NotificationStateEnum.Unavailable, reloaded.State);
            Assert.Equal(0, reloaded.ActorCount);
        }

        [Fact]
        public async Task EnqueueTargetUnavailableForExistingRecipientsAsync_ShouldQueueSingleBroadcastOutbox()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var initiator = CreateAccount("initiator");
            var recipient = CreateAccount("recipient");
            var owner = CreateAccount("owner");
            var postId = Guid.NewGuid();

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = NotificationAggregateKeys.PostReact(postId),
                State = NotificationStateEnum.Active,
                IsRead = false,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2),
                ActorCount = 1,
                EventCount = 1,
                LastActorId = owner.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = owner.AccountId,
                    Username = owner.Username,
                    FullName = owner.FullName,
                    AvatarUrl = owner.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = postId
            };

            await context.Accounts.AddRangeAsync(initiator, recipient, owner);
            await context.Notifications.AddAsync(notification);
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            await service.EnqueueTargetUnavailableForExistingRecipientsAsync(
                NotificationTypeEnum.PostReact,
                NotificationTargetKindEnum.Post,
                postId,
                initiator.AccountId,
                now);
            await context.SaveChangesAsync();

            var outboxes = await context.NotificationOutboxes
                .AsNoTracking()
                .ToListAsync();
            var outbox = Assert.Single(outboxes);
            Assert.Equal(NotificationOutboxEventTypes.TargetUnavailableBroadcast, outbox.EventType);
            Assert.Equal(initiator.AccountId, outbox.RecipientId);
        }

        [Fact]
        public async Task ProjectAsync_TargetUnavailableBroadcast_ShouldFanOutTargetUnavailableOutboxes()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;
            var postId = Guid.NewGuid();

            var initiator = CreateAccount("initiator");
            var recipientA = CreateAccount("recipient-a");
            var recipientB = CreateAccount("recipient-b");
            var actor = CreateAccount("actor");
            await context.Accounts.AddRangeAsync(initiator, recipientA, recipientB, actor);

            var notifications = new[]
            {
                new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    RecipientId = recipientA.AccountId,
                    Type = NotificationTypeEnum.PostReact,
                    AggregateKey = NotificationAggregateKeys.PostReact(postId),
                    State = NotificationStateEnum.Active,
                    IsRead = false,
                    CreatedAt = now.AddMinutes(-5),
                    LastEventAt = now.AddMinutes(-3),
                    UpdatedAt = now.AddMinutes(-3),
                    ActorCount = 1,
                    EventCount = 1,
                    LastActorId = actor.AccountId,
                    LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                    {
                        AccountId = actor.AccountId,
                        Username = actor.Username,
                        FullName = actor.FullName,
                        AvatarUrl = actor.AvatarUrl
                    }),
                    TargetKind = NotificationTargetKindEnum.Post,
                    TargetId = postId
                },
                new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    RecipientId = recipientB.AccountId,
                    Type = NotificationTypeEnum.PostReact,
                    AggregateKey = NotificationAggregateKeys.PostReact(postId),
                    State = NotificationStateEnum.Active,
                    IsRead = false,
                    CreatedAt = now.AddMinutes(-4),
                    LastEventAt = now.AddMinutes(-2),
                    UpdatedAt = now.AddMinutes(-2),
                    ActorCount = 1,
                    EventCount = 1,
                    LastActorId = actor.AccountId,
                    LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                    {
                        AccountId = actor.AccountId,
                        Username = actor.Username,
                        FullName = actor.FullName,
                        AvatarUrl = actor.AvatarUrl
                    }),
                    TargetKind = NotificationTargetKindEnum.Post,
                    TargetId = postId
                }
            };

            await context.Notifications.AddRangeAsync(notifications);
            await context.SaveChangesAsync();

            var payload = new NotificationTargetUnavailablePayload
            {
                Type = NotificationTypeEnum.PostReact,
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = postId,
                OccurredAt = now
            };
            var broadcastOutbox = new NotificationOutbox
            {
                OutboxId = Guid.NewGuid(),
                EventType = NotificationOutboxEventTypes.TargetUnavailableBroadcast,
                RecipientId = initiator.AccountId,
                PayloadJson = JsonSerializer.Serialize(payload),
                OccurredAt = now,
                Status = NotificationOutboxStatusEnum.Pending,
                NextRetryAt = now
            };

            var projector = new NotificationProjector(new NotificationRepository(context), context);
            var projectionResult = await projector.ProjectAsync(broadcastOutbox);

            Assert.Equal(NotificationProjectionActionEnum.None, projectionResult.Action);
            var generatedOutboxes = (await context.NotificationOutboxes
                    .AsNoTracking()
                    .Where(x => x.EventType == NotificationOutboxEventTypes.TargetUnavailable)
                    .ToListAsync())
                .Where(x => x.PayloadJson.Contains(postId.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.Equal(2, generatedOutboxes.Count);
        }

        [Fact]
        public async Task GetNotificationsAsync_WhenActorSnapshotNeedsFixup_ShouldPersistActorFields()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var owner = CreateAccount("owner");
            var actor = CreateAccount("actor");
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                PostCode = "POSTFIXUP123",
                Privacy = PostPrivacyEnum.Public,
                IsDeleted = false,
                CreatedAt = now.AddMinutes(-20)
            };

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = NotificationAggregateKeys.PostReact(post.PostId),
                State = NotificationStateEnum.Active,
                IsRead = false,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2),
                ActorCount = 2,
                EventCount = 2,
                LastActorId = null,
                LastActorSnapshot = null,
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = post.PostId
            };

            var contribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.PostReact,
                SourceId = actor.AccountId,
                ActorId = actor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2)
            };

            await context.Accounts.AddRangeAsync(recipient, owner, actor);
            await context.Posts.AddAsync(post);
            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var result = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "all" });

            Assert.Single(result.Items);
            var persisted = await context.Notifications
                .AsNoTracking()
                .SingleAsync(x => x.NotificationId == notification.NotificationId);
            Assert.Equal(1, persisted.ActorCount);
            Assert.Equal(1, persisted.EventCount);
            Assert.Equal(actor.AccountId, persisted.LastActorId);
            Assert.False(string.IsNullOrWhiteSpace(persisted.LastActorSnapshot));
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"notification-tests-{Guid.NewGuid()}")
                .Options;
            return new AppDbContext(options);
        }

        private static Account CreateAccount(string username)
        {
            return new Account
            {
                AccountId = Guid.NewGuid(),
                Username = username,
                FullName = $"{username} full",
                Email = $"{username}@test.local",
                RoleId = 1,
                Status = AccountStatusEnum.Active,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
