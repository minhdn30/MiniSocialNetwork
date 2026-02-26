using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Models;

namespace SocialNetwork.Infrastructure.Repositories.StoryViews
{
    public class StoryViewRepository : IStoryViewRepository
    {
        private readonly AppDbContext _context;

        public StoryViewRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddStoryViewsAsync(IEnumerable<StoryView> storyViews)
        {
            if (storyViews == null)
            {
                return;
            }

            var items = storyViews.ToList();
            if (items.Count == 0)
            {
                return;
            }

            await _context.StoryViews.AddRangeAsync(items);
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

            var userViewMap = await _context.StoryViews
                .AsNoTracking()
                .Where(v => v.ViewerAccountId == currentId)
                .ToDictionaryAsync(v => v.StoryId, v => v.ReactType);

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
                    IsViewedByCurrentUser = false // Will be updated below
                })
                .ToListAsync();

            foreach (var s in stories)
            {
                if (userViewMap.TryGetValue(s.StoryId, out var reactType))
                {
                    s.IsViewedByCurrentUser = true;
                    s.CurrentUserReactType = reactType;
                }
            }

            return stories;
        }

        public async Task<Dictionary<Guid, StoryViewSummaryModel>> GetStoryViewSummariesAsync(
            Guid authorId,
            IReadOnlyCollection<Guid> storyIds,
            int topCount)
        {
            if (storyIds == null || storyIds.Count == 0)
            {
                return new Dictionary<Guid, StoryViewSummaryModel>();
            }

            if (topCount <= 0)
            {
                topCount = 3;
            }

            var normalizedStoryIds = storyIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedStoryIds.Count == 0)
            {
                return new Dictionary<Guid, StoryViewSummaryModel>();
            }

            var result = normalizedStoryIds.ToDictionary(
                id => id,
                id => new StoryViewSummaryModel
                {
                    StoryId = id,
                    TotalViews = 0,
                    TopViewers = Array.Empty<StoryViewerBasicModel>()
                });

            var viewRows = await _context.StoryViews
                .AsNoTracking()
                .Where(v =>
                    normalizedStoryIds.Contains(v.StoryId) &&
                    v.ViewerAccountId != authorId &&
                    v.ViewerAccount.Status == AccountStatusEnum.Active)
                .Select(v => new
                {
                    v.StoryId,
                    v.ViewerAccountId,
                    v.ViewerAccount.Username,
                    v.ViewerAccount.FullName,
                    v.ViewerAccount.AvatarUrl,
                    v.ViewedAt,
                    v.ReactType
                })
                .ToListAsync();

            foreach (var grouped in viewRows.GroupBy(x => x.StoryId))
            {
                var topViewers = grouped
                    .OrderByDescending(x => x.ReactType.HasValue)
                    .ThenByDescending(x => x.ViewedAt)
                    .Take(topCount)
                    .Select(x => new StoryViewerBasicModel
                    {
                        AccountId = x.ViewerAccountId,
                        Username = x.Username,
                        FullName = x.FullName,
                        AvatarUrl = x.AvatarUrl,
                        ViewedAt = x.ViewedAt,
                        ReactType = (int?)x.ReactType
                    })
                    .ToList();

                result[grouped.Key] = new StoryViewSummaryModel
                {
                    StoryId = grouped.Key,
                    TotalViews = grouped.Count(),
                    TopViewers = topViewers
                };
            }

            return result;
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

        public async Task<HashSet<Guid>> GetViewedStoryIdsByViewerAsync(
            Guid viewerAccountId,
            IReadOnlyCollection<Guid> storyIds)
        {
            if (storyIds == null || storyIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            var normalizedStoryIds = storyIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedStoryIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            var viewedIds = await _context.StoryViews
                .AsNoTracking()
                .Where(v =>
                    v.ViewerAccountId == viewerAccountId &&
                    normalizedStoryIds.Contains(v.StoryId))
                .Select(v => v.StoryId)
                .ToListAsync();

            return viewedIds.ToHashSet();
        }

        public async Task<List<StoryRingStatsByAuthorModel>> GetStoryRingStatsByAuthorAsync(
            Guid currentId,
            IReadOnlyCollection<Guid> authorIds,
            DateTime nowUtc)
        {
            if (authorIds == null || authorIds.Count == 0)
            {
                return new List<StoryRingStatsByAuthorModel>();
            }

            var normalizedAuthorIds = authorIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedAuthorIds.Count == 0)
            {
                return new List<StoryRingStatsByAuthorModel>();
            }

            var viewedStoryIds = _context.StoryViews
                .AsNoTracking()
                .Where(v => v.ViewerAccountId == currentId)
                .Select(v => v.StoryId);

            return await BuildVisibleStoriesQuery(currentId, nowUtc)
                .Where(s => normalizedAuthorIds.Contains(s.AccountId))
                .Select(s => new
                {
                    s.AccountId,
                    IsViewed = viewedStoryIds.Contains(s.StoryId)
                })
                .GroupBy(x => x.AccountId)
                .Select(g => new StoryRingStatsByAuthorModel
                {
                    AccountId = g.Key,
                    VisibleCount = g.Count(),
                    UnseenCount = g.Count(x => !x.IsViewed)
                })
                .ToListAsync();
        }

        public async Task<StoryView?> GetStoryViewAsync(Guid storyId, Guid viewerAccountId)
        {
            return await _context.StoryViews
                .FirstOrDefaultAsync(v => v.StoryId == storyId && v.ViewerAccountId == viewerAccountId);
        }

        public async Task UpdateStoryViewAsync(StoryView storyView)
        {
            _context.StoryViews.Update(storyView);
            await Task.CompletedTask;
        }

        public async Task<(List<StoryViewerBasicModel> Items, int TotalItems)> GetStoryViewersPagedAsync(Guid storyId, int page, int pageSize)
        {
            var query = _context.StoryViews
                .AsNoTracking()
                .Where(v => v.StoryId == storyId && v.ViewerAccount.Status == AccountStatusEnum.Active);

            int totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(v => v.ReactType.HasValue)
                .ThenByDescending(v => v.ReactedAt)
                .ThenByDescending(v => v.ViewedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new StoryViewerBasicModel
                {
                    AccountId = v.ViewerAccountId,
                    Username = v.ViewerAccount.Username,
                    FullName = v.ViewerAccount.FullName,
                    AvatarUrl = v.ViewerAccount.AvatarUrl,
                    ViewedAt = v.ViewedAt,
                    ReactType = (int?)v.ReactType
                })
                .ToListAsync();

            return (items, totalItems);
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
                        s.Privacy == StoryPrivacyEnum.Public ||
                        (s.Privacy == StoryPrivacyEnum.FollowOnly &&
                         followedIds.Contains(s.AccountId))
                    ));
        }
    }
}
