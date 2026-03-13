using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Posts;
using System.Reflection;

namespace CloudM.Tests.Repositories
{
    public class PostRepositoryTests
    {
        [Fact]
        public async Task GetFeedPageAsync_WithSmallCandidateWindow_ContinuesAcrossWindowsWithoutDuplicates()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"post-repo-feed-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var repository = new PostRepository(context);
            var snapshotAt = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc);
            var currentId = Guid.NewGuid();

            var currentAccount = BuildAccount(currentId, "current-user");
            var author1 = BuildAccount(Guid.NewGuid(), "author-1");
            var author2 = BuildAccount(Guid.NewGuid(), "author-2");
            var author3 = BuildAccount(Guid.NewGuid(), "author-3");
            var author4 = BuildAccount(Guid.NewGuid(), "author-4");

            context.Accounts.AddRange(currentAccount, author1, author2, author3, author4);

            var posts = new[]
            {
                BuildPost(author1.AccountId, "FEED000001", snapshotAt.AddMinutes(-5)),
                BuildPost(author2.AccountId, "FEED000002", snapshotAt.AddMinutes(-10)),
                BuildPost(author3.AccountId, "FEED000003", snapshotAt.AddMinutes(-15)),
                BuildPost(author4.AccountId, "FEED000004", snapshotAt.AddMinutes(-20))
            };

            context.Posts.AddRange(posts);
            context.PostMedias.AddRange(posts.Select((post, index) => new PostMedia
            {
                MediaId = Guid.NewGuid(),
                PostId = post.PostId,
                MediaUrl = $"https://cdn.test/{index + 1}.jpg",
                Type = MediaTypeEnum.Image,
                CreatedAt = post.CreatedAt.AddSeconds(1)
            }));

            await context.SaveChangesAsync();

            var rankingProfile = new FeedRankingProfileModel
            {
                ProfileKey = "test-window",
                CandidateMultiplier = 1,
                CandidateMinCount = 2,
                CandidateMaxCount = 2,
                JitterBucketCount = 1
            }.Normalize("test-window");

            var firstCursor = new PostFeedCursorModel
            {
                SnapshotAt = snapshotAt,
                ProfileKey = rankingProfile.ProfileKey,
                SessionSeed = 17
            };

            var firstPage = await repository.GetFeedPageAsync(currentId, firstCursor, rankingProfile, 3);

            firstPage.Should().HaveCount(3);
            firstPage.Select(x => x.PostId).Should().OnlyHaveUniqueItems();
            firstPage.Select(x => x.PostId).Should().Equal(posts[0].PostId, posts[1].PostId, posts[2].PostId);

            var lastFirstPageItem = firstPage.Last();
            var secondCursor = new PostFeedCursorModel
            {
                SnapshotAt = snapshotAt,
                ProfileKey = rankingProfile.ProfileKey,
                SessionSeed = 17,
                Score = lastFirstPageItem.RankingScore,
                JitterRank = lastFirstPageItem.RankingJitterRank,
                CreatedAt = lastFirstPageItem.CreatedAt,
                PostId = lastFirstPageItem.PostId,
                WindowCursorCreatedAt = lastFirstPageItem.RankingWindowCursorCreatedAt,
                WindowCursorPostId = lastFirstPageItem.RankingWindowCursorPostId
            };

            var secondPage = await repository.GetFeedPageAsync(currentId, secondCursor, rankingProfile, 3);

            secondPage.Should().HaveCount(1);
            secondPage[0].PostId.Should().Be(posts[3].PostId);
            secondPage.Select(x => x.PostId)
                .Intersect(firstPage.Select(x => x.PostId))
                .Should()
                .BeEmpty();
        }

        [Fact]
        public async Task GetFeedPageAsync_UsesRankingSignalsAndHydratesInteractionFlags()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"post-repo-feed-ranking-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var repository = new PostRepository(context);
            var snapshotAt = new DateTime(2026, 3, 12, 9, 0, 0, DateTimeKind.Utc);
            var currentId = Guid.NewGuid();

            var currentAccount = BuildAccount(currentId, "current-user");
            var followedAuthor = BuildAccount(Guid.NewGuid(), "followed-author");
            var pendingAuthor = BuildAccount(Guid.NewGuid(), "pending-author");
            var activeUser1 = BuildAccount(Guid.NewGuid(), "active-user-1");
            var activeUser2 = BuildAccount(Guid.NewGuid(), "active-user-2");

            context.Accounts.AddRange(currentAccount, followedAuthor, pendingAuthor, activeUser1, activeUser2);

            var followedPost = BuildPost(followedAuthor.AccountId, "FEED100001", snapshotAt.AddHours(-2));
            var pendingPost = BuildPost(pendingAuthor.AccountId, "FEED100002", snapshotAt.AddHours(-2));

            context.Posts.AddRange(followedPost, pendingPost);
            context.PostMedias.AddRange(
                new PostMedia
                {
                    MediaId = Guid.NewGuid(),
                    PostId = followedPost.PostId,
                    MediaUrl = "https://cdn.test/followed.jpg",
                    Type = MediaTypeEnum.Image,
                    CreatedAt = followedPost.CreatedAt.AddSeconds(1)
                },
                new PostMedia
                {
                    MediaId = Guid.NewGuid(),
                    PostId = pendingPost.PostId,
                    MediaUrl = "https://cdn.test/pending.jpg",
                    Type = MediaTypeEnum.Image,
                    CreatedAt = pendingPost.CreatedAt.AddSeconds(1)
                });

            context.Follows.Add(new Follow
            {
                FollowerId = currentId,
                FollowedId = followedAuthor.AccountId,
                CreatedAt = snapshotAt.AddDays(-3)
            });

            context.FollowRequests.Add(new FollowRequest
            {
                RequesterId = currentId,
                TargetId = pendingAuthor.AccountId,
                CreatedAt = snapshotAt.AddDays(-1)
            });

            context.PostReacts.AddRange(
                new PostReact
                {
                    PostId = pendingPost.PostId,
                    AccountId = currentId,
                    ReactType = ReactEnum.Like,
                    CreatedAt = snapshotAt.AddMinutes(-30)
                },
                new PostReact
                {
                    PostId = pendingPost.PostId,
                    AccountId = activeUser1.AccountId,
                    ReactType = ReactEnum.Love,
                    CreatedAt = snapshotAt.AddMinutes(-25)
                },
                new PostReact
                {
                    PostId = pendingPost.PostId,
                    AccountId = activeUser2.AccountId,
                    ReactType = ReactEnum.Haha,
                    CreatedAt = snapshotAt.AddMinutes(-20)
                });

            var rootComment = new Comment
            {
                CommentId = Guid.NewGuid(),
                PostId = pendingPost.PostId,
                AccountId = activeUser1.AccountId,
                Content = "root comment",
                CreatedAt = snapshotAt.AddMinutes(-15)
            };
            var replyComment = new Comment
            {
                CommentId = Guid.NewGuid(),
                PostId = pendingPost.PostId,
                AccountId = activeUser2.AccountId,
                Content = "reply comment",
                ParentCommentId = rootComment.CommentId,
                CreatedAt = snapshotAt.AddMinutes(-10)
            };

            context.Comments.AddRange(rootComment, replyComment);
            context.PostSaves.Add(new PostSave
            {
                PostId = pendingPost.PostId,
                AccountId = currentId,
                CreatedAt = snapshotAt.AddMinutes(-5)
            });

            await context.SaveChangesAsync();

            var rankingProfile = new FeedRankingProfileModel
            {
                ProfileKey = "test-ranked",
                CandidateMultiplier = 1,
                CandidateMinCount = 2,
                CandidateMaxCount = 2,
                JitterBucketCount = 1,
                FreshnessDay1Score = 0m,
                FreshnessDay3Score = 0m,
                FreshnessDay7Score = 0m,
                FreshnessDay14Score = 0m,
                FreshnessDay30Score = 0m,
                FreshnessOlderScore = 0m,
                FollowBonus = 20m,
                SelfFallbackBonus = 0m,
                DiscoverBonus = 0m,
                AffinityHotBonus = 0m,
                AffinityWarmBonus = 0m,
                ReactWeight = 1m,
                RootCommentWeight = 1m,
                ReplyWeight = 1m,
                ReactCountCap = 10,
                RootCommentCountCap = 10,
                ReplyCountCap = 10
            }.Normalize("test-ranked");

            var cursor = new PostFeedCursorModel
            {
                SnapshotAt = snapshotAt,
                ProfileKey = rankingProfile.ProfileKey,
                SessionSeed = 17
            };

            var feed = await repository.GetFeedPageAsync(currentId, cursor, rankingProfile, 2);

            feed.Should().HaveCount(2);
            feed[0].PostId.Should().Be(followedPost.PostId);
            feed[1].PostId.Should().Be(pendingPost.PostId);
            feed[0].Author.Username.Should().Be(followedAuthor.Username);
            feed[1].Content.Should().Be(pendingPost.Content);
            feed[0].Author.IsFollowedByCurrentUser.Should().BeTrue();
            feed[0].Author.IsFollowRequestPendingByCurrentUser.Should().BeFalse();
            feed[1].Author.IsFollowedByCurrentUser.Should().BeFalse();
            feed[1].Author.IsFollowRequestPendingByCurrentUser.Should().BeTrue();
            feed[1].ReactCount.Should().Be(3);
            feed[1].CommentCount.Should().Be(1);
            feed[1].ReplyCount.Should().Be(1);
            feed[1].IsReactedByCurrentUser.Should().BeTrue();
            feed[1].IsSavedByCurrentUser.Should().BeTrue();
            feed[1].MediaCount.Should().Be(1);
        }

        [Fact]
        public async Task HydrateFeedPostsAsync_WhenPostBecomesInvisible_ShouldDropItFromFeed()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"post-repo-hydrate-visibility-{Guid.NewGuid()}")
                .Options;

            await using var context = new AppDbContext(options);
            var repository = new PostRepository(context);
            var snapshotAt = new DateTime(2026, 3, 13, 9, 0, 0, DateTimeKind.Utc);
            var currentId = Guid.NewGuid();

            var currentAccount = BuildAccount(currentId, "current-user");
            var author = BuildAccount(Guid.NewGuid(), "author-hidden");
            var post = BuildPost(author.AccountId, "FEED200001", snapshotAt.AddMinutes(-30));

            context.Accounts.AddRange(currentAccount, author);
            context.Posts.Add(post);
            context.PostMedias.Add(new PostMedia
            {
                MediaId = Guid.NewGuid(),
                PostId = post.PostId,
                MediaUrl = "https://cdn.test/hidden.jpg",
                Type = MediaTypeEnum.Image,
                CreatedAt = post.CreatedAt.AddSeconds(1)
            });
            await context.SaveChangesAsync();

            var rankedRow = CreateRankedFeedPostRow(post, author);

            post.IsDeleted = true;
            await context.SaveChangesAsync();

            var feed = await InvokeHydrateFeedPostsAsync(repository, currentId, snapshotAt, rankedRow);

            feed.Should().BeEmpty();
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

        private static Post BuildPost(Guid accountId, string postCode, DateTime createdAt)
        {
            return new Post
            {
                PostId = Guid.NewGuid(),
                AccountId = accountId,
                PostCode = postCode,
                Content = postCode,
                Privacy = PostPrivacyEnum.Public,
                FeedAspectRatio = AspectRatioEnum.Original,
                CreatedAt = createdAt,
                IsDeleted = false
            };
        }

        private static object CreateRankedFeedPostRow(Post post, Account author)
        {
            var rankedType = typeof(PostRepository).GetNestedType("RankedFeedPostRow", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("RankedFeedPostRow type was not found.");

            var row = Activator.CreateInstance(rankedType, nonPublic: true)
                ?? throw new InvalidOperationException("RankedFeedPostRow instance could not be created.");

            rankedType.GetProperty("PostId")!.SetValue(row, post.PostId);
            rankedType.GetProperty("PostCode")!.SetValue(row, post.PostCode);
            rankedType.GetProperty("Content")!.SetValue(row, post.Content);
            rankedType.GetProperty("Privacy")!.SetValue(row, post.Privacy);
            rankedType.GetProperty("FeedAspectRatio")!.SetValue(row, post.FeedAspectRatio);
            rankedType.GetProperty("CreatedAt")!.SetValue(row, post.CreatedAt);
            rankedType.GetProperty("AuthorAccountId")!.SetValue(row, author.AccountId);
            rankedType.GetProperty("AuthorUsername")!.SetValue(row, author.Username);
            rankedType.GetProperty("AuthorFullName")!.SetValue(row, author.FullName);
            rankedType.GetProperty("AuthorAvatarUrl")!.SetValue(row, author.AvatarUrl);
            rankedType.GetProperty("AuthorStatus")!.SetValue(row, author.Status);
            rankedType.GetProperty("MediaCount")!.SetValue(row, 1);
            rankedType.GetProperty("ReactCount")!.SetValue(row, 0);
            rankedType.GetProperty("CommentCount")!.SetValue(row, 0);
            rankedType.GetProperty("ReplyCount")!.SetValue(row, 0);
            rankedType.GetProperty("IsReactedByCurrentUser")!.SetValue(row, false);
            rankedType.GetProperty("IsSavedByCurrentUser")!.SetValue(row, false);
            rankedType.GetProperty("IsOwner")!.SetValue(row, false);
            rankedType.GetProperty("IsFollowedAuthor")!.SetValue(row, false);
            rankedType.GetProperty("IsFollowRequestPendingAuthor")!.SetValue(row, false);
            rankedType.GetProperty("RankingScore")!.SetValue(row, 0m);
            rankedType.GetProperty("RankingJitterRank")!.SetValue(row, 0L);
            rankedType.GetProperty("RankingWindowCursorCreatedAt")!.SetValue(row, null);
            rankedType.GetProperty("RankingWindowCursorPostId")!.SetValue(row, null);
            rankedType.GetProperty("LegacyScore")!.SetValue(row, 0d);

            return row;
        }

        private static async Task<List<PostFeedModel>> InvokeHydrateFeedPostsAsync(
            PostRepository repository,
            Guid currentId,
            DateTime snapshotAt,
            params object[] rankedRows)
        {
            var rankedType = typeof(PostRepository).GetNestedType("RankedFeedPostRow", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("RankedFeedPostRow type was not found.");
            var rankedListType = typeof(List<>).MakeGenericType(rankedType);
            var rankedList = Activator.CreateInstance(rankedListType)
                ?? throw new InvalidOperationException("RankedFeedPostRow list could not be created.");
            var addMethod = rankedListType.GetMethod("Add")
                ?? throw new InvalidOperationException("RankedFeedPostRow list add method was not found.");

            foreach (var rankedRow in rankedRows)
            {
                addMethod.Invoke(rankedList, new[] { rankedRow });
            }

            var hydrateMethod = typeof(PostRepository).GetMethod("HydrateFeedPostsAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("HydrateFeedPostsAsync method was not found.");
            var task = (Task)hydrateMethod.Invoke(repository, new object[] { rankedList, currentId, (DateTime?)snapshotAt })!
                ?? throw new InvalidOperationException("HydrateFeedPostsAsync invocation failed.");

            await task;

            return (List<PostFeedModel>)(task.GetType().GetProperty("Result")?.GetValue(task)
                ?? throw new InvalidOperationException("HydrateFeedPostsAsync result was not available."));
        }
    }
}
