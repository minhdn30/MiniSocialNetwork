using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Posts;

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
    }
}
