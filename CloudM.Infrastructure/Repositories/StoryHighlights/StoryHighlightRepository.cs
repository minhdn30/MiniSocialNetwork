using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.StoryHighlights
{
    public class StoryHighlightRepository : IStoryHighlightRepository
    {
        private readonly AppDbContext _context;

        public StoryHighlightRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<int> CountGroupsByOwnerAsync(Guid ownerId)
        {
            return _context.StoryHighlightGroups
                .AsNoTracking()
                .CountAsync(g =>
                    g.AccountId == ownerId &&
                    g.Items.Any(i =>
                        !i.Story.IsDeleted &&
                        i.Story.AccountId == ownerId));
        }

        public Task<int> CountEffectiveStoriesInGroupAsync(Guid groupId)
        {
            return _context.StoryHighlightItems
                .AsNoTracking()
                .CountAsync(i =>
                    i.StoryHighlightGroupId == groupId &&
                    !i.Story.IsDeleted &&
                    i.Story.AccountId == i.StoryHighlightGroup.AccountId);
        }

        public Task<StoryHighlightGroup?> GetGroupByIdAsync(Guid groupId)
        {
            return _context.StoryHighlightGroups
                .FirstOrDefaultAsync(g => g.StoryHighlightGroupId == groupId);
        }

        public Task<StoryHighlightGroup?> GetGroupByIdByOwnerAsync(Guid groupId, Guid ownerId)
        {
            return _context.StoryHighlightGroups
                .FirstOrDefaultAsync(g =>
                    g.StoryHighlightGroupId == groupId &&
                    g.AccountId == ownerId);
        }

        public Task<List<StoryHighlightGroup>> GetGroupsByOwnerContainingStoryAsync(Guid ownerId, Guid storyId)
        {
            return _context.StoryHighlightGroups
                .AsNoTracking()
                .Where(g =>
                    g.AccountId == ownerId &&
                    g.Items.Any(i => i.StoryId == storyId))
                .ToListAsync();
        }

        public async Task<bool> TryRemoveGroupIfEffectivelyEmptyAsync(Guid groupId, Guid ownerId)
        {
            var deletedRows = await _context.StoryHighlightGroups
                .Where(g =>
                    g.StoryHighlightGroupId == groupId &&
                    g.AccountId == ownerId &&
                    !g.Items.Any(i =>
                        !i.Story.IsDeleted &&
                        i.Story.AccountId == ownerId))
                .ExecuteDeleteAsync();

            return deletedRows > 0;
        }

        public async Task<List<StoryHighlightGroupListItemModel>> GetHighlightGroupsByOwnerAsync(Guid ownerId)
        {
            var groups = await _context.StoryHighlightGroups
                .AsNoTracking()
                .Where(g => g.AccountId == ownerId)
                .Select(g => new
                {
                    g.StoryHighlightGroupId,
                    g.AccountId,
                    g.Name,
                    g.CoverImageUrl,
                    g.CreatedAt,
                    g.UpdatedAt,
                    StoryCount = g.Items.Count(i =>
                        !i.Story.IsDeleted &&
                        i.Story.AccountId == ownerId)
                })
                .Where(x => x.StoryCount > 0)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.StoryHighlightGroupId)
                .ToListAsync();

            if (groups.Count == 0)
            {
                return new List<StoryHighlightGroupListItemModel>();
            }

            var groupIds = groups.Select(x => x.StoryHighlightGroupId).ToList();
            var firstStories = await _context.StoryHighlightItems
                .AsNoTracking()
                .Where(i =>
                    groupIds.Contains(i.StoryHighlightGroupId) &&
                    !i.Story.IsDeleted &&
                    i.Story.AccountId == ownerId)
                .OrderBy(i => i.StoryHighlightGroupId)
                .ThenBy(i => i.Story.CreatedAt)
                .ThenBy(i => i.StoryId)
                .Select(i => new
                {
                    i.StoryHighlightGroupId,
                    i.Story.StoryId,
                    i.Story.ContentType,
                    i.Story.MediaUrl,
                    i.Story.TextContent,
                    i.Story.BackgroundColorKey,
                    i.Story.FontTextKey,
                    i.Story.FontSizeKey,
                    i.Story.TextColorKey,
                    i.Story.CreatedAt,
                    i.Story.ExpiresAt
                })
                .ToListAsync();

            var fallbackMap = new Dictionary<Guid, StoryHighlightArchiveCandidateModel>();
            foreach (var row in firstStories)
            {
                if (fallbackMap.ContainsKey(row.StoryHighlightGroupId))
                {
                    continue;
                }

                fallbackMap[row.StoryHighlightGroupId] = new StoryHighlightArchiveCandidateModel
                {
                    StoryId = row.StoryId,
                    ContentType = row.ContentType,
                    MediaUrl = row.MediaUrl,
                    TextContent = row.TextContent,
                    BackgroundColorKey = row.BackgroundColorKey,
                    FontTextKey = row.FontTextKey,
                    FontSizeKey = row.FontSizeKey,
                    TextColorKey = row.TextColorKey,
                    CreatedAt = row.CreatedAt,
                    ExpiresAt = row.ExpiresAt
                };
            }

            return groups.Select(g =>
            {
                fallbackMap.TryGetValue(g.StoryHighlightGroupId, out var fallbackStory);
                return new StoryHighlightGroupListItemModel
                {
                    StoryHighlightGroupId = g.StoryHighlightGroupId,
                    AccountId = g.AccountId,
                    Name = g.Name,
                    CoverImageUrl = g.CoverImageUrl,
                    CreatedAt = g.CreatedAt,
                    UpdatedAt = g.UpdatedAt,
                    StoryCount = g.StoryCount,
                    FallbackStory = fallbackStory
                };
            }).ToList();
        }

        public async Task<List<StoryHighlightStoryItemModel>> GetHighlightStoriesByGroupAsync(Guid groupId, Guid? viewerId)
        {
            var baseQuery = _context.StoryHighlightItems
                .AsNoTracking()
                .Where(i =>
                    i.StoryHighlightGroupId == groupId &&
                    !i.Story.IsDeleted &&
                    i.Story.AccountId == i.StoryHighlightGroup.AccountId)
                .OrderBy(i => i.Story.CreatedAt)
                .ThenBy(i => i.StoryId);

            if (!viewerId.HasValue)
            {
                return await baseQuery
                    .Select(i => new StoryHighlightStoryItemModel
                    {
                        StoryId = i.Story.StoryId,
                        AccountId = i.Story.AccountId,
                        ContentType = i.Story.ContentType,
                        MediaUrl = i.Story.MediaUrl,
                        TextContent = i.Story.TextContent,
                        BackgroundColorKey = i.Story.BackgroundColorKey,
                        FontTextKey = i.Story.FontTextKey,
                        FontSizeKey = i.Story.FontSizeKey,
                        TextColorKey = i.Story.TextColorKey,
                        Privacy = i.Story.Privacy,
                        CreatedAt = i.Story.CreatedAt,
                        ExpiresAt = i.Story.ExpiresAt,
                        IsViewedByCurrentUser = false,
                        CurrentUserReactType = null
                    })
                    .ToListAsync();
            }

            var viewerIdValue = viewerId.Value;
            var viewerStoryViews = _context.StoryViews
                .AsNoTracking()
                .Where(v => v.ViewerAccountId == viewerIdValue);

            return await (
                from item in baseQuery
                join view in viewerStoryViews
                    on item.StoryId equals view.StoryId into viewJoin
                from matchedView in viewJoin.DefaultIfEmpty()
                select new StoryHighlightStoryItemModel
                {
                    StoryId = item.Story.StoryId,
                    AccountId = item.Story.AccountId,
                    ContentType = item.Story.ContentType,
                    MediaUrl = item.Story.MediaUrl,
                    TextContent = item.Story.TextContent,
                    BackgroundColorKey = item.Story.BackgroundColorKey,
                    FontTextKey = item.Story.FontTextKey,
                    FontSizeKey = item.Story.FontSizeKey,
                    TextColorKey = item.Story.TextColorKey,
                    Privacy = item.Story.Privacy,
                    CreatedAt = item.Story.CreatedAt,
                    ExpiresAt = item.Story.ExpiresAt,
                    IsViewedByCurrentUser = matchedView != null,
                    CurrentUserReactType = matchedView != null ? matchedView.ReactType : null
                })
                .ToListAsync();
        }

        public async Task<(List<StoryHighlightArchiveCandidateModel> Items, int TotalItems)> GetArchiveCandidatesAsync(
            Guid ownerId,
            int page,
            int pageSize,
            Guid? excludeGroupId)
        {
            var baseQuery = _context.Stories
                .AsNoTracking()
                .Where(s =>
                    s.AccountId == ownerId &&
                    !s.IsDeleted);

            if (excludeGroupId.HasValue && excludeGroupId.Value != Guid.Empty)
            {
                var excludedStoryIds = _context.StoryHighlightItems
                    .AsNoTracking()
                    .Where(i => i.StoryHighlightGroupId == excludeGroupId.Value)
                    .Select(i => i.StoryId);

                baseQuery = baseQuery.Where(s => !excludedStoryIds.Contains(s.StoryId));
            }

            var totalItems = await baseQuery.CountAsync();
            var items = await baseQuery
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.StoryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new StoryHighlightArchiveCandidateModel
                {
                    StoryId = s.StoryId,
                    ContentType = s.ContentType,
                    MediaUrl = s.MediaUrl,
                    TextContent = s.TextContent,
                    BackgroundColorKey = s.BackgroundColorKey,
                    FontTextKey = s.FontTextKey,
                    FontSizeKey = s.FontSizeKey,
                    TextColorKey = s.TextColorKey,
                    CreatedAt = s.CreatedAt,
                    ExpiresAt = s.ExpiresAt
                })
                .ToListAsync();

            return (items, totalItems);
        }

        public async Task<List<StoryHighlightArchiveCandidateModel>> GetArchiveStoriesByIdsForOwnerAsync(
            Guid ownerId,
            IReadOnlyCollection<Guid> storyIds)
        {
            if (storyIds == null || storyIds.Count == 0)
            {
                return new List<StoryHighlightArchiveCandidateModel>();
            }

            var normalizedStoryIds = storyIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedStoryIds.Count == 0)
            {
                return new List<StoryHighlightArchiveCandidateModel>();
            }

            return await _context.Stories
                .AsNoTracking()
                .Where(s =>
                    s.AccountId == ownerId &&
                    !s.IsDeleted &&
                    normalizedStoryIds.Contains(s.StoryId))
                .OrderBy(s => s.CreatedAt)
                .ThenBy(s => s.StoryId)
                .Select(s => new StoryHighlightArchiveCandidateModel
                {
                    StoryId = s.StoryId,
                    ContentType = s.ContentType,
                    MediaUrl = s.MediaUrl,
                    TextContent = s.TextContent,
                    BackgroundColorKey = s.BackgroundColorKey,
                    FontTextKey = s.FontTextKey,
                    FontSizeKey = s.FontSizeKey,
                    TextColorKey = s.TextColorKey,
                    CreatedAt = s.CreatedAt,
                    ExpiresAt = s.ExpiresAt
                })
                .ToListAsync();
        }

        public async Task<HashSet<Guid>> GetExistingStoryIdsInGroupAsync(Guid groupId, IReadOnlyCollection<Guid> storyIds)
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

            var existingStoryIds = await _context.StoryHighlightItems
                .AsNoTracking()
                .Where(i =>
                    i.StoryHighlightGroupId == groupId &&
                    normalizedStoryIds.Contains(i.StoryId))
                .Select(i => i.StoryId)
                .ToListAsync();

            return existingStoryIds.ToHashSet();
        }

        public Task AddGroupAsync(StoryHighlightGroup group)
        {
            return _context.StoryHighlightGroups.AddAsync(group).AsTask();
        }

        public Task AddItemsAsync(IEnumerable<StoryHighlightItem> items)
        {
            return _context.StoryHighlightItems.AddRangeAsync(items);
        }

        public async Task RemoveItemAsync(Guid groupId, Guid storyId)
        {
            var item = await _context.StoryHighlightItems
                .FirstOrDefaultAsync(i =>
                    i.StoryHighlightGroupId == groupId &&
                    i.StoryId == storyId);

            if (item == null)
            {
                return;
            }

            _context.StoryHighlightItems.Remove(item);
        }

        public Task RemoveGroupAsync(StoryHighlightGroup group)
        {
            _context.StoryHighlightGroups.Remove(group);
            return Task.CompletedTask;
        }

        public Task UpdateGroupAsync(StoryHighlightGroup group)
        {
            _context.StoryHighlightGroups.Update(group);
            return Task.CompletedTask;
        }
    }
}
