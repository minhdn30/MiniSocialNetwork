using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Models;

namespace SocialNetwork.Infrastructure.Repositories.Stories
{
    public class StoryRepository : IStoryRepository
    {
        private readonly AppDbContext _context;

        public StoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddStoryAsync(Story story)
        {
            await _context.Stories.AddAsync(story);
        }

        public async Task<Story?> GetStoryByIdAsync(Guid storyId)
        {
            return await _context.Stories.FirstOrDefaultAsync(s => s.StoryId == storyId);
        }

        public async Task<Story?> GetViewableStoryByIdAsync(Guid currentId, Guid storyId, DateTime nowUtc)
        {
            return await BuildVisibleStoriesQuery(currentId, nowUtc)
                .FirstOrDefaultAsync(s => s.StoryId == storyId);
        }

        public async Task<(List<StoryAuthorVisibleSummaryModel> Items, int TotalItems)> GetViewableAuthorSummariesAsync(
            Guid currentId,
            DateTime nowUtc,
            int page,
            int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var viewedStoryIds = _context.StoryViews
                .AsNoTracking()
                .Where(v => v.ViewerAccountId == currentId)
                .Select(v => v.StoryId);

            var viewCountByAuthor = _context.StoryViews
                .AsNoTracking()
                .Where(v => v.ViewerAccountId == currentId)
                .GroupBy(v => v.Story.AccountId)
                .Select(g => new { AuthorId = g.Key, ViewCount = g.Count() });

            var baseQuery = BuildVisibleStoriesQuery(currentId, nowUtc)
                .Select(s => new
                {
                    s.AccountId,
                    s.Account.Username,
                    s.Account.FullName,
                    s.Account.AvatarUrl,
                    s.CreatedAt,
                    IsViewed = viewedStoryIds.Contains(s.StoryId)
                })
                .GroupBy(x => new
                {
                    x.AccountId,
                    x.Username,
                    x.FullName,
                    x.AvatarUrl
                })
                .Select(g => new StoryAuthorVisibleSummaryModel
                {
                    AccountId = g.Key.AccountId,
                    Username = g.Key.Username,
                    FullName = g.Key.FullName,
                    AvatarUrl = g.Key.AvatarUrl,
                    LatestStoryCreatedAt = g.Max(x => x.CreatedAt),
                    ActiveStoryCount = g.Count(),
                    UnseenCount = g.Count(x => !x.IsViewed),
                    ViewFrequencyScore = viewCountByAuthor
                        .Where(v => v.AuthorId == g.Key.AccountId)
                        .Select(v => v.ViewCount)
                        .FirstOrDefault()
                })
                .OrderBy(x => x.AccountId == currentId ? 0 : 1)
                .ThenBy(x => x.UnseenCount > 0 ? 0 : 1)
                .ThenByDescending(x => x.ViewFrequencyScore)
                .ThenByDescending(x => x.LatestStoryCreatedAt);

            var totalItems = await baseQuery.CountAsync();
            var items = await baseQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalItems);
        }

        public async Task<List<StoryActiveItemModel>> GetActiveStoriesByAuthorAsync(
            Guid currentId,
            Guid authorId,
            DateTime nowUtc)
        {
            if (authorId == Guid.Empty)
            {
                return new List<StoryActiveItemModel>();
            }

            var stories = await BuildVisibleStoriesQuery(currentId, nowUtc)
                .Where(s => s.AccountId == authorId)
                .OrderBy(s => s.CreatedAt)
                .ThenBy(s => s.StoryId)
                .Select(s => new StoryActiveItemModel
                {
                    StoryId = s.StoryId,
                    AccountId = s.AccountId,
                    Username = s.Account.Username,
                    FullName = s.Account.FullName,
                    AvatarUrl = s.Account.AvatarUrl,
                    ContentType = s.ContentType,
                    MediaUrl = s.MediaUrl,
                    TextContent = s.TextContent,
                    BackgroundColorKey = s.BackgroundColorKey,
                    FontTextKey = s.FontTextKey,
                    FontSizeKey = s.FontSizeKey,
                    TextColorKey = s.TextColorKey,
                    Privacy = s.Privacy,
                    CreatedAt = s.CreatedAt,
                    ExpiresAt = s.ExpiresAt,
                    IsViewedByCurrentUser = false
                })
                .ToListAsync();

            if (stories.Count == 0)
            {
                return stories;
            }

            var storyIds = stories.Select(s => s.StoryId).ToList();
            var userViewMap = await _context.StoryViews
                .AsNoTracking()
                .Where(v =>
                    v.ViewerAccountId == currentId &&
                    storyIds.Contains(v.StoryId))
                .ToDictionaryAsync(v => v.StoryId, v => v.ReactType);

            foreach (var story in stories)
            {
                if (userViewMap.TryGetValue(story.StoryId, out var reactType))
                {
                    story.IsViewedByCurrentUser = true;
                    story.CurrentUserReactType = reactType;
                }
            }

            return stories;
        }

        public async Task<List<Guid>> GetViewableStoryIdsAsync(
            Guid currentId,
            IReadOnlyCollection<Guid> storyIds,
            DateTime nowUtc)
        {
            if (storyIds == null || storyIds.Count == 0)
            {
                return new List<Guid>();
            }

            var normalizedStoryIds = storyIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedStoryIds.Count == 0)
            {
                return new List<Guid>();
            }

            return await BuildVisibleStoriesQuery(currentId, nowUtc)
                .Where(s => normalizedStoryIds.Contains(s.StoryId))
                .Select(s => s.StoryId)
                .ToListAsync();
        }

        public async Task<Guid?> ResolveAuthorIdByStoryIdAsync(
            Guid currentId,
            Guid storyId,
            DateTime nowUtc)
        {
            if (storyId == Guid.Empty)
            {
                return null;
            }

            return await BuildVisibleStoriesQuery(currentId, nowUtc)
                .Where(s => s.StoryId == storyId)
                .Select(s => (Guid?)s.AccountId)
                .FirstOrDefaultAsync();
        }

        public Task UpdateStoryAsync(Story story)
        {
            _context.Stories.Update(story);
            return Task.CompletedTask;
        }

        public async Task<bool> HasRecentStoryAsync(Guid accountId, SocialNetwork.Domain.Enums.StoryContentTypeEnum contentType, TimeSpan window)
        {
            var cutoff = DateTime.UtcNow.Subtract(window);
            return await _context.Stories
                .AnyAsync(s => s.AccountId == accountId && 
                               s.ContentType == contentType && 
                               s.CreatedAt >= cutoff &&
                               !s.IsDeleted);
        }

        public async Task<bool> ExistsAndActiveAsync(Guid storyId)
        {
            return await _context.Stories
                .AnyAsync(s => s.StoryId == storyId &&
                               s.ExpiresAt > DateTime.UtcNow &&
                               !s.IsDeleted);
        }

        public async Task<HashSet<Guid>> GetActiveStoryIdsAsync(IEnumerable<Guid> storyIds)
        {
            var idList = storyIds.ToList();
            if (!idList.Any()) return new HashSet<Guid>();

            var activeIds = await _context.Stories
                .AsNoTracking()
                .Where(s => idList.Contains(s.StoryId) &&
                            s.ExpiresAt > DateTime.UtcNow &&
                            !s.IsDeleted)
                .Select(s => s.StoryId)
                .ToListAsync();

            return activeIds.ToHashSet();
        }

        private IQueryable<Story> BuildVisibleStoriesQuery(Guid currentId, DateTime nowUtc)
        {
            var followedIds = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            return _context.Stories
                .AsNoTracking()
                .Where(s =>
                    !s.IsDeleted &&
                    s.ExpiresAt > nowUtc &&
                    s.Account.Status == AccountStatusEnum.Active &&
                    (
                        s.AccountId == currentId ||
                        (
                            followedIds.Contains(s.AccountId) &&
                            (
                                s.Privacy == StoryPrivacyEnum.Public ||
                                s.Privacy == StoryPrivacyEnum.FollowOnly
                            )
                        )
                    ));
        }
    }
}
