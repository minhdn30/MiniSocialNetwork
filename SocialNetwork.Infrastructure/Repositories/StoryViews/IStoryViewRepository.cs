using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Models;

namespace SocialNetwork.Infrastructure.Repositories.StoryViews
{
    public interface IStoryViewRepository
    {
        Task AddStoryViewsAsync(IEnumerable<StoryView> storyViews);
        Task<int> AddStoryViewsIgnoreConflictAsync(IEnumerable<StoryView> storyViews);
        Task<bool> TryAddStoryViewAsync(StoryView storyView);
        Task<Dictionary<Guid, StoryViewSummaryModel>> GetStoryViewSummariesAsync(
            Guid authorId,
            IReadOnlyCollection<Guid> storyIds,
            int topCount);
        Task<HashSet<Guid>> GetViewedStoryIdsByViewerAsync(
            Guid viewerAccountId,
            IReadOnlyCollection<Guid> storyIds);
        Task<List<StoryRingStatsByAuthorModel>> GetStoryRingStatsByAuthorAsync(
            Guid currentId,
            IReadOnlyCollection<Guid> authorIds,
            DateTime nowUtc);
        Task<StoryView?> GetStoryViewAsync(Guid storyId, Guid viewerAccountId);
        Task UpdateStoryViewAsync(StoryView storyView);
        Task<(List<StoryViewerBasicModel> Items, int TotalItems)> GetStoryViewersPagedAsync(Guid storyId, int page, int pageSize);
    }
}
