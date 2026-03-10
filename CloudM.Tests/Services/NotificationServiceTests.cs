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
        public async Task GetNotificationsAsync_ExpiredOwnedStoryTarget_ShouldRemainAvailableForOwnerArchive()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var owner = CreateAccount("story-owner");
            var actor = CreateAccount("actor");
            var story = new Story
            {
                StoryId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                ContentType = StoryContentTypeEnum.Image,
                MediaUrl = "https://cdn/story-archive.jpg",
                Privacy = StoryPrivacyEnum.Public,
                CreatedAt = now.AddDays(-2),
                ExpiresAt = now.AddHours(-1),
                IsDeleted = false
            };

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = owner.AccountId,
                Type = NotificationTypeEnum.StoryReact,
                AggregateKey = NotificationAggregateKeys.StoryReact(story.StoryId),
                State = NotificationStateEnum.Active,
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

            await context.Accounts.AddRangeAsync(owner, actor);
            await context.Stories.AddAsync(story);
            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var result = await service.GetNotificationsAsync(
                owner.AccountId,
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
        public async Task GetNotificationsAsync_ShouldExcludeFollowRequestFromListAndUnreadCount()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var actor = CreateAccount("actor");
            var owner = CreateAccount("owner");
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                PostCode = "POSTEXCLUDEFOLLOWREQ",
                Privacy = PostPrivacyEnum.Public,
                IsDeleted = false,
                CreatedAt = now.AddMinutes(-10)
            };

            await context.Accounts.AddRangeAsync(recipient, actor, owner);
            await context.Posts.AddAsync(post);

            var followRequestNotification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.FollowRequest,
                AggregateKey = NotificationAggregateKeys.FollowRequest(actor.AccountId),
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-5),
                LastEventAt = now.AddMinutes(-4),
                UpdatedAt = now.AddMinutes(-4),
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
                TargetKind = NotificationTargetKindEnum.Account,
                TargetId = actor.AccountId
            };

            var postReactNotification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = NotificationAggregateKeys.PostReact(post.PostId),
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-3),
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
                TargetId = post.PostId
            };

            var contribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = postReactNotification.NotificationId,
                SourceType = NotificationSourceTypeEnum.PostReact,
                SourceId = actor.AccountId,
                ActorId = actor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2)
            };

            await context.Notifications.AddRangeAsync(followRequestNotification, postReactNotification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var listResult = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "all" });
            var unreadCount = await service.GetUnreadCountAsync(recipient.AccountId);

            var item = Assert.Single(listResult.Items);
            Assert.Equal((int)NotificationTypeEnum.PostReact, item.Type);
            Assert.Equal(1, unreadCount);
        }

        [Fact]
        public async Task GetNotificationsAsync_ShouldReturnPendingFollowRequestCountAndIncludeUnreadFollowRequestsInBadgeCount()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var activeRequester = CreateAccount("active-requester");
            var inactiveRequester = CreateAccount("inactive-requester");
            var actor = CreateAccount("actor");
            var owner = CreateAccount("owner");
            inactiveRequester.Status = AccountStatusEnum.Inactive;

            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                PostCode = "POSTFOLLOWREQCOUNT",
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
                CreatedAt = now.AddMinutes(-3),
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

            var followRequests = new[]
            {
                new FollowRequest
                {
                    RequesterId = activeRequester.AccountId,
                    TargetId = recipient.AccountId,
                    CreatedAt = now.AddMinutes(-4)
                },
                new FollowRequest
                {
                    RequesterId = inactiveRequester.AccountId,
                    TargetId = recipient.AccountId,
                    CreatedAt = now.AddMinutes(-5)
                }
            };

            await context.Accounts.AddRangeAsync(
                recipient,
                activeRequester,
                inactiveRequester,
                actor,
                owner);
            await context.Posts.AddAsync(post);
            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.FollowRequests.AddRangeAsync(followRequests);
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var listResult = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "all" });
            var unreadCount = await service.GetUnreadCountAsync(recipient.AccountId);

            Assert.Single(listResult.Items);
            Assert.Equal(1, listResult.FollowRequestCount);
            Assert.Equal(2, unreadCount);
        }

        [Fact]
        public async Task GetUnreadSummaryAsync_ShouldSplitNotificationAndFollowRequestCountsBySeenWatermark()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var actor = CreateAccount("actor");
            var owner = CreateAccount("owner");
            var requesterA = CreateAccount("requester-a");
            var requesterB = CreateAccount("requester-b");
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                PostCode = "POSTREADSTATE01",
                Privacy = PostPrivacyEnum.Public,
                IsDeleted = false,
                CreatedAt = now.AddMinutes(-20)
            };

            var olderNotification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = $"{NotificationAggregateKeys.PostReact(post.PostId)}-older",
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-7),
                LastEventAt = now.AddMinutes(-6),
                UpdatedAt = now.AddMinutes(-6),
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

            var newerNotification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostComment,
                AggregateKey = $"{NotificationAggregateKeys.PostComment(post.PostId)}-newer",
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-3),
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
                TargetId = post.PostId
            };

            await context.Accounts.AddRangeAsync(recipient, actor, owner, requesterA, requesterB);
            await context.Posts.AddAsync(post);
            await context.Notifications.AddRangeAsync(olderNotification, newerNotification);
            await context.FollowRequests.AddRangeAsync(
                new FollowRequest
                {
                    RequesterId = requesterA.AccountId,
                    TargetId = recipient.AccountId,
                    CreatedAt = now.AddMinutes(-5)
                },
                new FollowRequest
                {
                    RequesterId = requesterB.AccountId,
                    TargetId = recipient.AccountId,
                    CreatedAt = now.AddMinutes(-1)
                });
            await context.NotificationReadStates.AddAsync(new NotificationReadState
            {
                AccountId = recipient.AccountId,
                LastNotificationsSeenAt = now.AddMinutes(-4),
                LastFollowRequestsSeenAt = now.AddMinutes(-3),
                CreatedAt = now.AddMinutes(-4),
                UpdatedAt = now.AddMinutes(-4)
            });
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var summary = await service.GetUnreadSummaryAsync(recipient.AccountId);
            var listResult = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "all" });

            Assert.Equal(2, summary.PendingFollowRequestCount);
            Assert.Equal(recipient.AccountId, summary.AccountId);
            Assert.Equal(1, summary.NotificationUnreadCount);
            Assert.Equal(1, summary.FollowRequestUnreadCount);
            Assert.Equal(2, summary.Count);
            Assert.Equal(now.AddMinutes(-4), summary.LastNotificationsSeenAt);
            Assert.Equal(now.AddMinutes(-3), summary.LastFollowRequestsSeenAt);
            Assert.Equal(recipient.AccountId, listResult.AccountId);
            Assert.Equal(summary.Count, listResult.Count);
            Assert.Equal(summary.NotificationUnreadCount, listResult.NotificationUnreadCount);
            Assert.Equal(summary.FollowRequestUnreadCount, listResult.FollowRequestUnreadCount);
            Assert.Equal(summary.PendingFollowRequestCount, listResult.PendingFollowRequestCount);
            Assert.Equal(summary.LastNotificationsSeenAt, listResult.LastNotificationsSeenAt);
            Assert.Equal(summary.LastFollowRequestsSeenAt, listResult.LastFollowRequestsSeenAt);
            Assert.Equal(2, listResult.FollowRequestCount);
        }

        [Fact]
        public async Task GetNotificationsAsync_UnreadFilter_ShouldUseLastNotificationsSeenAt()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var actor = CreateAccount("actor");
            var owner = CreateAccount("owner");
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                PostCode = "POSTREADSTATE02",
                Privacy = PostPrivacyEnum.Public,
                IsDeleted = false,
                CreatedAt = now.AddMinutes(-10)
            };

            var olderNotification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = $"{NotificationAggregateKeys.PostReact(post.PostId)}-older-filter",
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-6),
                LastEventAt = now.AddMinutes(-5),
                UpdatedAt = now.AddMinutes(-5),
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

            var newerNotification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostComment,
                AggregateKey = $"{NotificationAggregateKeys.PostComment(post.PostId)}-newer-filter",
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-2),
                LastEventAt = now.AddMinutes(-1),
                UpdatedAt = now.AddMinutes(-1),
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

            await context.Accounts.AddRangeAsync(recipient, actor, owner);
            await context.Posts.AddAsync(post);
            await context.Notifications.AddRangeAsync(olderNotification, newerNotification);
            await context.NotificationReadStates.AddAsync(new NotificationReadState
            {
                AccountId = recipient.AccountId,
                LastNotificationsSeenAt = now.AddMinutes(-3),
                CreatedAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3)
            });
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var result = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "unread" });

            var item = Assert.Single(result.Items);
            Assert.Equal(newerNotification.NotificationId, item.NotificationId);
        }

        [Fact]
        public async Task GetUnreadCountAsync_ShouldIgnoreCorrectionTimestampWhenActiveContributionIsOlderThanSeen()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var actor = CreateAccount("actor");
            var owner = CreateAccount("owner");
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                PostCode = "POSTCORRECTION01",
                Privacy = PostPrivacyEnum.Public,
                IsDeleted = false,
                CreatedAt = now.AddMinutes(-20)
            };

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostComment,
                AggregateKey = $"{NotificationAggregateKeys.PostComment(post.PostId)}-correction-count",
                State = NotificationStateEnum.Unavailable,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-1),
                UpdatedAt = now.AddMinutes(-1),
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
                SourceType = NotificationSourceTypeEnum.Comment,
                SourceId = Guid.NewGuid(),
                ActorId = actor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-8),
                UpdatedAt = now.AddMinutes(-8)
            };

            await context.Accounts.AddRangeAsync(recipient, actor, owner);
            await context.Posts.AddAsync(post);
            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.NotificationReadStates.AddAsync(new NotificationReadState
            {
                AccountId = recipient.AccountId,
                LastNotificationsSeenAt = now.AddMinutes(-3),
                CreatedAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3)
            });
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var unreadCount = await service.GetUnreadCountAsync(recipient.AccountId);
            var unreadList = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "unread" });

            Assert.Equal(0, unreadCount);
            Assert.Empty(unreadList.Items);
        }

        [Fact]
        public async Task GetNotificationsAsync_ShouldUseActiveContributionTimestampForSeenState()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var actor = CreateAccount("actor");
            var owner = CreateAccount("owner");
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                PostCode = "POSTSEENSTATE01",
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
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-1),
                UpdatedAt = now.AddMinutes(-1),
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
                SourceId = Guid.NewGuid(),
                ActorId = actor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-8),
                UpdatedAt = now.AddMinutes(-8)
            };

            await context.Accounts.AddRangeAsync(recipient, actor, owner);
            await context.Posts.AddAsync(post);
            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.NotificationReadStates.AddAsync(new NotificationReadState
            {
                AccountId = recipient.AccountId,
                LastNotificationsSeenAt = now.AddMinutes(-3),
                CreatedAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3)
            });
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var result = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "all" });

            var item = Assert.Single(result.Items);
            Assert.True(item.IsSeenByCurrentState);
            Assert.True(item.TracksUnreadState);
            Assert.Equal(now.AddMinutes(-8), item.SeenStateTimestamp);
            Assert.Equal(now.AddMinutes(-1), item.LastEventAt);
        }

        [Fact]
        public async Task GetNotificationsAsync_WhenNotificationHasOnlyInactiveContributions_ShouldNotTrackUnreadState()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var actor = CreateAccount("actor");
            var owner = CreateAccount("owner");
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = owner.AccountId,
                PostCode = "POSTKEEPUNREAD01",
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
                State = NotificationStateEnum.Unavailable,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-1),
                UpdatedAt = now.AddMinutes(-1),
                ActorCount = 0,
                EventCount = 0,
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
                SourceId = Guid.NewGuid(),
                ActorId = actor.AccountId,
                IsActive = false,
                CreatedAt = now.AddMinutes(-8),
                UpdatedAt = now.AddMinutes(-1)
            };

            await context.Accounts.AddRangeAsync(recipient, actor, owner);
            await context.Posts.AddAsync(post);
            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(contribution);
            await context.NotificationReadStates.AddAsync(new NotificationReadState
            {
                AccountId = recipient.AccountId,
                LastNotificationsSeenAt = now.AddMinutes(-3),
                CreatedAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3)
            });
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var unreadCount = await service.GetUnreadCountAsync(recipient.AccountId);
            var unreadList = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "unread" });
            var allList = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "all" });

            Assert.Equal(0, unreadCount);
            Assert.Empty(unreadList.Items);

            var item = Assert.Single(allList.Items);
            Assert.True(item.IsSeenByCurrentState);
            Assert.False(item.TracksUnreadState);
            Assert.Equal(now.AddMinutes(-1), item.SeenStateTimestamp);
            Assert.Equal(now.AddMinutes(-1), item.LastEventAt);
        }

        [Fact]
        public async Task UpdateReadStateAsync_ShouldAdvanceMonotonically()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            await context.Accounts.AddAsync(recipient);
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var initialSummary = await service.UpdateReadStateAsync(
                recipient.AccountId,
                new NotificationReadStateRequest
                {
                    NotificationsSeenAt = now.AddMinutes(-4),
                    FollowRequestsSeenAt = now.AddMinutes(-2)
                });

            var replaySummary = await service.UpdateReadStateAsync(
                recipient.AccountId,
                new NotificationReadStateRequest
                {
                    NotificationsSeenAt = now.AddMinutes(-6),
                    FollowRequestsSeenAt = now.AddMinutes(-5)
                });

            var advancedSummary = await service.UpdateReadStateAsync(
                recipient.AccountId,
                new NotificationReadStateRequest
                {
                    NotificationsSeenAt = now.AddMinutes(-1)
                });

            var persistedState = await context.NotificationReadStates
                .AsNoTracking()
                .SingleAsync(x => x.AccountId == recipient.AccountId);

            Assert.Equal(now.AddMinutes(-4), initialSummary.LastNotificationsSeenAt);
            Assert.Equal(now.AddMinutes(-2), initialSummary.LastFollowRequestsSeenAt);
            Assert.Equal(now.AddMinutes(-4), replaySummary.LastNotificationsSeenAt);
            Assert.Equal(now.AddMinutes(-2), replaySummary.LastFollowRequestsSeenAt);
            Assert.Equal(now.AddMinutes(-1), advancedSummary.LastNotificationsSeenAt);
            Assert.Equal(now.AddMinutes(-2), advancedSummary.LastFollowRequestsSeenAt);
            Assert.Equal(now.AddMinutes(-1), persistedState.LastNotificationsSeenAt);
            Assert.Equal(now.AddMinutes(-2), persistedState.LastFollowRequestsSeenAt);
        }

        [Fact]
        public async Task UpdateReadStateAsync_ShouldIgnoreFutureTimestamps()
        {
            await using var context = CreateContext();
            var recipient = CreateAccount("recipient");
            await context.Accounts.AddAsync(recipient);
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var summary = await service.UpdateReadStateAsync(
                recipient.AccountId,
                new NotificationReadStateRequest
                {
                    NotificationsSeenAt = DateTime.UtcNow.AddDays(3),
                    FollowRequestsSeenAt = DateTime.UtcNow.AddDays(3)
                });

            Assert.Equal(recipient.AccountId, summary.AccountId);
            Assert.Null(summary.LastNotificationsSeenAt);
            Assert.Null(summary.LastFollowRequestsSeenAt);
            Assert.Empty(await context.NotificationReadStates.AsNoTracking().ToListAsync());
        }

        [Fact]
        public async Task ProjectAsync_ShouldAttachOutboxMetadataToProjectionResult()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var actor = CreateAccount("actor");
            await context.Accounts.AddRangeAsync(recipient, actor);
            await context.SaveChangesAsync();

            var projector = new NotificationProjector(new NotificationRepository(context), context);
            var postId = Guid.NewGuid();
            var outbox = new NotificationOutbox
            {
                OutboxId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                EventType = NotificationOutboxEventTypes.AggregateChanged,
                PayloadJson = JsonSerializer.Serialize(new NotificationAggregateChangedPayload
                {
                    Action = NotificationAggregateActionEnum.Upsert,
                    Type = NotificationTypeEnum.PostReact,
                    AggregateKey = NotificationAggregateKeys.PostReact(postId),
                    SourceType = NotificationSourceTypeEnum.PostReact,
                    SourceId = Guid.NewGuid(),
                    ActorId = actor.AccountId,
                    TargetKind = NotificationTargetKindEnum.Post,
                    TargetId = postId,
                    KeepWhenEmpty = false,
                    OccurredAt = now.AddMinutes(-2)
                }),
                OccurredAt = now.AddMinutes(-2)
            };

            var result = await projector.ProjectAsync(outbox);

            Assert.Equal(NotificationProjectionActionEnum.Upsert, result.Action);
            Assert.Equal(outbox.OutboxId, result.EventId);
            Assert.Equal(outbox.OccurredAt, result.OccurredAt);
            Assert.True(result.AffectsUnread);
            Assert.NotNull(result.NotificationId);
            Assert.NotNull(result.Toast);
            Assert.Equal((int)NotificationTypeEnum.PostReact, result.Toast!.Type);
            Assert.Equal(actor.AccountId, result.Toast.ActorAccountId);
            Assert.Equal(actor.Username, result.Toast.ActorUsername);
            Assert.Equal(actor.FullName, result.Toast.ActorFullName);
            Assert.Equal(actor.AvatarUrl, result.Toast.ActorAvatarUrl);
            Assert.Equal((int)NotificationTargetKindEnum.Post, result.Toast.TargetKind);
            Assert.Equal(postId, result.Toast.TargetId);
            Assert.False(result.Toast.CanOpen);
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
            Assert.False(projectionResult.AffectsUnread);
            var reloaded = await context.Notifications
                .AsNoTracking()
                .SingleAsync(x => x.NotificationId == notification.NotificationId);
            Assert.Equal(NotificationStateEnum.Unavailable, reloaded.State);
            Assert.Equal(0, reloaded.ActorCount);
        }

        [Fact]
        public async Task ProjectAsync_DeactivateAll_ShouldRemoveNotification()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;
            var postId = Guid.NewGuid();
            var commentId = Guid.NewGuid();

            var recipient = CreateAccount("recipient");
            var actorOne = CreateAccount("actor-one");
            var actorTwo = CreateAccount("actor-two");
            await context.Accounts.AddRangeAsync(recipient, actorOne, actorTwo);
            await context.Posts.AddAsync(new Post
            {
                PostId = postId,
                AccountId = recipient.AccountId,
                Privacy = PostPrivacyEnum.Public,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1),
                IsDeleted = false
            });

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.CommentReact,
                AggregateKey = NotificationAggregateKeys.CommentReact(commentId),
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2),
                ActorCount = 2,
                EventCount = 2,
                LastActorId = actorTwo.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = actorTwo.AccountId,
                    Username = actorTwo.Username,
                    FullName = actorTwo.FullName,
                    AvatarUrl = actorTwo.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = postId
            };

            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddRangeAsync(
                new NotificationContribution
                {
                    ContributionId = Guid.NewGuid(),
                    NotificationId = notification.NotificationId,
                    SourceType = NotificationSourceTypeEnum.CommentReact,
                    SourceId = actorOne.AccountId,
                    ActorId = actorOne.AccountId,
                    IsActive = true,
                    CreatedAt = now.AddMinutes(-3),
                    UpdatedAt = now.AddMinutes(-3)
                },
                new NotificationContribution
                {
                    ContributionId = Guid.NewGuid(),
                    NotificationId = notification.NotificationId,
                    SourceType = NotificationSourceTypeEnum.CommentReact,
                    SourceId = actorTwo.AccountId,
                    ActorId = actorTwo.AccountId,
                    IsActive = true,
                    CreatedAt = now.AddMinutes(-2),
                    UpdatedAt = now.AddMinutes(-2)
                });
            await context.SaveChangesAsync();

            var payload = new NotificationAggregateChangedPayload
            {
                Action = NotificationAggregateActionEnum.DeactivateAll,
                Type = NotificationTypeEnum.CommentReact,
                AggregateKey = notification.AggregateKey,
                SourceType = NotificationSourceTypeEnum.CommentReact,
                SourceId = Guid.Empty,
                ActorId = null,
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = postId,
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
        public async Task ProjectAsync_DeactivateWithRemainingActiveContribution_ShouldRecomputeLastEventAt()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;
            var postId = Guid.NewGuid();

            var recipient = CreateAccount("recipient");
            var actorA = CreateAccount("actor-a");
            var actorB = CreateAccount("actor-b");
            await context.Accounts.AddRangeAsync(recipient, actorA, actorB);

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostTag,
                AggregateKey = NotificationAggregateKeys.PostTag(postId),
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2),
                ActorCount = 2,
                EventCount = 2,
                LastActorId = actorB.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = actorB.AccountId,
                    Username = actorB.Username,
                    FullName = actorB.FullName,
                    AvatarUrl = actorB.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = postId
            };

            var contributionA = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.PostTag,
                SourceId = postId,
                ActorId = actorA.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-6),
                UpdatedAt = now.AddMinutes(-6)
            };

            var contributionB = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.PostTag,
                SourceId = Guid.NewGuid(),
                ActorId = actorB.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2)
            };

            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddRangeAsync(contributionA, contributionB);
            await context.SaveChangesAsync();

            var payload = new NotificationAggregateChangedPayload
            {
                Action = NotificationAggregateActionEnum.Deactivate,
                Type = NotificationTypeEnum.PostTag,
                AggregateKey = notification.AggregateKey,
                SourceType = NotificationSourceTypeEnum.PostTag,
                SourceId = contributionB.SourceId,
                ActorId = actorB.AccountId,
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
            Assert.False(projectionResult.AffectsUnread);
            var reloaded = await context.Notifications
                .AsNoTracking()
                .SingleAsync(x => x.NotificationId == notification.NotificationId);
            Assert.Equal(now.AddMinutes(-6), reloaded.LastEventAt);
            Assert.Equal(actorA.AccountId, reloaded.LastActorId);
            Assert.Equal(1, reloaded.ActorCount);
            Assert.Equal(1, reloaded.EventCount);
        }

        [Fact]
        public async Task ProjectAsync_TargetUnavailable_ShouldPreserveLastEventAt()
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
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = NotificationAggregateKeys.PostReact(postId),
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-4),
                UpdatedAt = now.AddMinutes(-4),
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

            await context.Notifications.AddAsync(notification);
            await context.SaveChangesAsync();

            var payload = new NotificationTargetUnavailablePayload
            {
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = notification.AggregateKey,
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = postId,
                OccurredAt = now
            };

            var outbox = new NotificationOutbox
            {
                OutboxId = Guid.NewGuid(),
                EventType = NotificationOutboxEventTypes.TargetUnavailable,
                RecipientId = recipient.AccountId,
                PayloadJson = JsonSerializer.Serialize(payload),
                OccurredAt = now,
                Status = NotificationOutboxStatusEnum.Pending,
                NextRetryAt = now
            };

            var projector = new NotificationProjector(new NotificationRepository(context), context);
            var projectionResult = await projector.ProjectAsync(outbox);

            Assert.Equal(NotificationProjectionActionEnum.Upsert, projectionResult.Action);
            Assert.False(projectionResult.AffectsUnread);
            var reloaded = await context.Notifications
                .AsNoTracking()
                .SingleAsync(x => x.NotificationId == notification.NotificationId);
            Assert.Equal(now.AddMinutes(-4), reloaded.LastEventAt);
            Assert.Equal(NotificationStateEnum.Unavailable, reloaded.State);
        }

        [Fact]
        public async Task ProjectAsync_OutOfOrderUpsert_ShouldNotAffectUnread()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;
            var postId = Guid.NewGuid();

            var recipient = CreateAccount("recipient");
            var actorA = CreateAccount("actor-a");
            var actorB = CreateAccount("actor-b");
            await context.Accounts.AddRangeAsync(recipient, actorA, actorB);

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = NotificationAggregateKeys.PostReact(postId),
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2),
                ActorCount = 1,
                EventCount = 1,
                LastActorId = actorA.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = actorA.AccountId,
                    Username = actorA.Username,
                    FullName = actorA.FullName,
                    AvatarUrl = actorA.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = postId
            };

            var existingContribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.PostReact,
                SourceId = Guid.NewGuid(),
                ActorId = actorA.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2)
            };

            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddAsync(existingContribution);
            await context.SaveChangesAsync();

            var payload = new NotificationAggregateChangedPayload
            {
                Action = NotificationAggregateActionEnum.Upsert,
                Type = NotificationTypeEnum.PostReact,
                AggregateKey = notification.AggregateKey,
                SourceType = NotificationSourceTypeEnum.PostReact,
                SourceId = Guid.NewGuid(),
                ActorId = actorB.AccountId,
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = postId,
                KeepWhenEmpty = false,
                OccurredAt = now.AddMinutes(-6)
            };

            var outbox = new NotificationOutbox
            {
                OutboxId = Guid.NewGuid(),
                EventType = NotificationOutboxEventTypes.AggregateChanged,
                RecipientId = recipient.AccountId,
                PayloadJson = JsonSerializer.Serialize(payload),
                OccurredAt = payload.OccurredAt,
                Status = NotificationOutboxStatusEnum.Pending,
                NextRetryAt = payload.OccurredAt
            };

            var projector = new NotificationProjector(new NotificationRepository(context), context);
            var projectionResult = await projector.ProjectAsync(outbox);

            Assert.Equal(NotificationProjectionActionEnum.Upsert, projectionResult.Action);
            Assert.False(projectionResult.AffectsUnread);
            Assert.Null(projectionResult.Toast);

            var reloaded = await context.Notifications
                .AsNoTracking()
                .SingleAsync(x => x.NotificationId == notification.NotificationId);
            Assert.Equal(now.AddMinutes(-2), reloaded.LastEventAt);
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

        [Fact]
        public async Task GetNotificationsAsync_FollowSummaryWithMultipleActors_ShouldDisableOpen()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var actorA = CreateAccount("actor-a");
            var actorB = CreateAccount("actor-b");

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.Follow,
                AggregateKey = NotificationAggregateKeys.FollowAutoAcceptSummary(recipient.AccountId),
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-1),
                UpdatedAt = now.AddMinutes(-1),
                ActorCount = 2,
                EventCount = 2,
                LastActorId = actorB.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = actorB.AccountId,
                    Username = actorB.Username,
                    FullName = actorB.FullName,
                    AvatarUrl = actorB.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Account,
                TargetId = actorB.AccountId
            };

            var contributionA = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.FollowRelation,
                SourceId = actorA.AccountId,
                ActorId = actorA.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3)
            };

            var contributionB = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.FollowRelation,
                SourceId = actorB.AccountId,
                ActorId = actorB.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-1),
                UpdatedAt = now.AddMinutes(-1)
            };

            await context.Accounts.AddRangeAsync(recipient, actorA, actorB);
            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddRangeAsync(contributionA, contributionB);
            await context.SaveChangesAsync();

            var service = new NotificationService(
                new NotificationOutboxRepository(context),
                new NotificationRepository(context),
                context);

            var result = await service.GetNotificationsAsync(
                recipient.AccountId,
                new NotificationCursorRequest { Limit = 20, Filter = "all" });

            var item = Assert.Single(result.Items);
            Assert.Equal((int)NotificationTypeEnum.Follow, item.Type);
            Assert.False(item.CanOpen);
            Assert.Contains("and 1 others followed you", item.Text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetNotificationsAsync_FollowSummaryWithSingleActor_ShouldUseLiveActorAsTarget()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var remainingActor = CreateAccount("remaining-actor");
            var staleTargetActor = CreateAccount("stale-target");

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.Follow,
                AggregateKey = NotificationAggregateKeys.FollowAutoAcceptSummary(recipient.AccountId),
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-10),
                LastEventAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2),
                ActorCount = 1,
                EventCount = 1,
                LastActorId = remainingActor.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = remainingActor.AccountId,
                    Username = remainingActor.Username,
                    FullName = remainingActor.FullName,
                    AvatarUrl = remainingActor.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Account,
                TargetId = staleTargetActor.AccountId
            };

            var contribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.FollowRelation,
                SourceId = remainingActor.AccountId,
                ActorId = remainingActor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2)
            };

            await context.Accounts.AddRangeAsync(recipient, remainingActor, staleTargetActor);
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
            Assert.True(item.CanOpen);
            Assert.Equal(remainingActor.AccountId, item.TargetId);
            Assert.NotNull(item.Actor);
            Assert.Equal(remainingActor.AccountId, item.Actor!.AccountId);
        }

        [Fact]
        public async Task GetNotificationsAsync_PostComment_ShouldExposeTargetCommentId()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var actor = CreateAccount("actor");
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = recipient.AccountId,
                PostCode = "POSTCMT00001",
                Privacy = PostPrivacyEnum.Public,
                IsDeleted = false,
                CreatedAt = now.AddDays(-1)
            };
            var comment = new Comment
            {
                CommentId = Guid.NewGuid(),
                PostId = post.PostId,
                AccountId = actor.AccountId,
                Content = "target comment",
                CreatedAt = now.AddMinutes(-3)
            };

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostComment,
                AggregateKey = NotificationAggregateKeys.PostComment(post.PostId),
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-3),
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
                TargetId = post.PostId
            };

            var contribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.Comment,
                SourceId = comment.CommentId,
                ActorId = actor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2)
            };

            await context.Accounts.AddRangeAsync(recipient, actor);
            await context.Posts.AddAsync(post);
            await context.Comments.AddAsync(comment);
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
            Assert.Equal(comment.CommentId, item.TargetCommentId);
            Assert.Null(item.ParentCommentId);
        }

        [Fact]
        public async Task GetNotificationsAsync_PostComment_ShouldResolveTargetCommentIdFromLatestActiveActor()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var activeActor = CreateAccount("active-actor");
            var inactiveActor = CreateAccount("inactive-actor");
            inactiveActor.Status = AccountStatusEnum.Inactive;
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = recipient.AccountId,
                PostCode = "POSTCMT00002",
                Privacy = PostPrivacyEnum.Public,
                IsDeleted = false,
                CreatedAt = now.AddDays(-1)
            };
            var activeComment = new Comment
            {
                CommentId = Guid.NewGuid(),
                PostId = post.PostId,
                AccountId = activeActor.AccountId,
                Content = "active target comment",
                CreatedAt = now.AddMinutes(-4)
            };
            var inactiveComment = new Comment
            {
                CommentId = Guid.NewGuid(),
                PostId = post.PostId,
                AccountId = inactiveActor.AccountId,
                Content = "inactive target comment",
                CreatedAt = now.AddMinutes(-2)
            };

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.PostComment,
                AggregateKey = NotificationAggregateKeys.PostComment(post.PostId),
                State = NotificationStateEnum.Active,
                CreatedAt = now.AddMinutes(-5),
                LastEventAt = now.AddMinutes(-1),
                UpdatedAt = now.AddMinutes(-1),
                ActorCount = 1,
                EventCount = 2,
                LastActorId = inactiveActor.AccountId,
                LastActorSnapshot = JsonSerializer.Serialize(new NotificationActorSnapshot
                {
                    AccountId = inactiveActor.AccountId,
                    Username = inactiveActor.Username,
                    FullName = inactiveActor.FullName,
                    AvatarUrl = inactiveActor.AvatarUrl
                }),
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = post.PostId
            };

            var activeContribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.Comment,
                SourceId = activeComment.CommentId,
                ActorId = activeActor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-4),
                UpdatedAt = now.AddMinutes(-4)
            };
            var inactiveContribution = new NotificationContribution
            {
                ContributionId = Guid.NewGuid(),
                NotificationId = notification.NotificationId,
                SourceType = NotificationSourceTypeEnum.Comment,
                SourceId = inactiveComment.CommentId,
                ActorId = inactiveActor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-1)
            };

            await context.Accounts.AddRangeAsync(recipient, activeActor, inactiveActor);
            await context.Posts.AddAsync(post);
            await context.Comments.AddRangeAsync(activeComment, inactiveComment);
            await context.Notifications.AddAsync(notification);
            await context.NotificationContributions.AddRangeAsync(activeContribution, inactiveContribution);
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
            Assert.Equal(activeActor.AccountId, item.Actor!.AccountId);
            Assert.Equal(activeComment.CommentId, item.TargetCommentId);
            Assert.Null(item.ParentCommentId);
        }

        [Fact]
        public async Task GetNotificationsAsync_ReplyReact_ShouldExposeParentCommentId()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;

            var recipient = CreateAccount("recipient");
            var actor = CreateAccount("actor");
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = recipient.AccountId,
                PostCode = "POSTRPL00001",
                Privacy = PostPrivacyEnum.Public,
                IsDeleted = false,
                CreatedAt = now.AddDays(-1)
            };
            var parentComment = new Comment
            {
                CommentId = Guid.NewGuid(),
                PostId = post.PostId,
                AccountId = recipient.AccountId,
                Content = "parent comment",
                CreatedAt = now.AddMinutes(-10)
            };
            var reply = new Comment
            {
                CommentId = Guid.NewGuid(),
                PostId = post.PostId,
                AccountId = recipient.AccountId,
                ParentCommentId = parentComment.CommentId,
                Content = "target reply",
                CreatedAt = now.AddMinutes(-5)
            };

            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientId = recipient.AccountId,
                Type = NotificationTypeEnum.ReplyReact,
                AggregateKey = NotificationAggregateKeys.ReplyReact(reply.CommentId),
                State = NotificationStateEnum.Active,
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
                SourceType = NotificationSourceTypeEnum.ReplyReact,
                SourceId = actor.AccountId,
                ActorId = actor.AccountId,
                IsActive = true,
                CreatedAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3)
            };

            await context.Accounts.AddRangeAsync(recipient, actor);
            await context.Posts.AddAsync(post);
            await context.Comments.AddRangeAsync(parentComment, reply);
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
            Assert.Equal(reply.CommentId, item.TargetCommentId);
            Assert.Equal(parentComment.CommentId, item.ParentCommentId);
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
                RoleId = (int)RoleEnum.User,
                Status = AccountStatusEnum.Active,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
