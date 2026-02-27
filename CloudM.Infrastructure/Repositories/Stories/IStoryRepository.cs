using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.Stories
{
    public interface IStoryRepository
    {
        Task AddStoryAsync(Story story);
        Task<Story?> GetStoryByIdAsync(Guid storyId);
        Task<Story?> GetViewableStoryByIdAsync(Guid currentId, Guid storyId, DateTime nowUtc);
        Task<(List<StoryAuthorVisibleSummaryModel> Items, int TotalItems)> GetViewableAuthorSummariesAsync(
            Guid currentId,
            DateTime nowUtc,
            int page,
            int pageSize);
        Task<(List<StoryArchiveItemModel> Items, int TotalItems)> GetArchivedStoriesByOwnerAsync(
            Guid ownerId,
            DateTime nowUtc,
            int page,
            int pageSize);
        Task<List<StoryActiveItemModel>> GetActiveStoriesByAuthorAsync(
            Guid currentId,
            Guid authorId,
            DateTime nowUtc);
        Task<List<Guid>> GetViewableStoryIdsAsync(
            Guid currentId,
            IReadOnlyCollection<Guid> storyIds,
            DateTime nowUtc);
        Task<Guid?> ResolveAuthorIdByStoryIdAsync(
            Guid currentId,
            Guid storyId,
            DateTime nowUtc);
        Task UpdateStoryAsync(Story story);
        Task<bool> HasRecentStoryAsync(Guid accountId, StoryContentTypeEnum contentType, TimeSpan window);
        Task<bool> ExistsAndActiveAsync(Guid storyId);
        Task<HashSet<Guid>> GetActiveStoryIdsAsync(IEnumerable<Guid> storyIds);
    }
}
