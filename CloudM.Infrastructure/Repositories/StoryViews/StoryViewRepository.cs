using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;
using System.Text.Json;

namespace CloudM.Infrastructure.Repositories.StoryViews
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

        public async Task<int> AddStoryViewsIgnoreConflictAsync(IEnumerable<StoryView> storyViews)
        {
            if (storyViews == null)
            {
                return 0;
            }

            var items = storyViews
                .Where(v => v != null)
                .ToList();

            if (items.Count == 0)
            {
                return 0;
            }

            var payload = JsonSerializer.Serialize(
                items.Select(v => new
                {
                    v.StoryId,
                    v.ViewerAccountId,
                    v.ViewedAt,
                    ReactType = (int?)v.ReactType,
                    v.ReactedAt
                }));

            return await _context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""StoryViews"" (""StoryId"", ""ViewerAccountId"", ""ViewedAt"", ""ReactType"", ""ReactedAt"")
                SELECT
                    rows.""StoryId"",
                    rows.""ViewerAccountId"",
                    rows.""ViewedAt"",
                    rows.""ReactType"",
                    rows.""ReactedAt""
                FROM jsonb_to_recordset({payload}::jsonb) AS rows(
                    ""StoryId"" uuid,
                    ""ViewerAccountId"" uuid,
                    ""ViewedAt"" timestamp with time zone,
                    ""ReactType"" integer,
                    ""ReactedAt"" timestamp with time zone
                )
                ON CONFLICT (""StoryId"", ""ViewerAccountId"") DO NOTHING");
        }

        public async Task<bool> TryAddStoryViewAsync(StoryView storyView)
        {
            if (storyView == null)
            {
                return false;
            }

            var affected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""StoryViews"" (""StoryId"", ""ViewerAccountId"", ""ViewedAt"", ""ReactType"", ""ReactedAt"")
                VALUES ({storyView.StoryId}, {storyView.ViewerAccountId}, {storyView.ViewedAt}, {(int?)storyView.ReactType}, {storyView.ReactedAt})
                ON CONFLICT (""StoryId"", ""ViewerAccountId"") DO NOTHING");

            return affected > 0;
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
                    TotalReacts = 0,
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
                    TotalReacts = grouped.Count(x => x.ReactType.HasValue),
                    TopViewers = topViewers
                };
            }

            return result;
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
