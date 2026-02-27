using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Models;

namespace SocialNetwork.Infrastructure.Repositories.StoryHighlights
{
    public interface IStoryHighlightRepository
    {
        Task<int> CountGroupsByOwnerAsync(Guid ownerId);
        Task<int> CountEffectiveStoriesInGroupAsync(Guid groupId);
        Task<StoryHighlightGroup?> GetGroupByIdAsync(Guid groupId);
        Task<StoryHighlightGroup?> GetGroupByIdByOwnerAsync(Guid groupId, Guid ownerId);
        Task<List<StoryHighlightGroup>> GetGroupsByOwnerContainingStoryAsync(Guid ownerId, Guid storyId);
        Task<bool> TryRemoveGroupIfEffectivelyEmptyAsync(Guid groupId, Guid ownerId);
        Task<List<StoryHighlightGroupListItemModel>> GetHighlightGroupsByOwnerAsync(Guid ownerId);
        Task<List<StoryHighlightStoryItemModel>> GetHighlightStoriesByGroupAsync(Guid groupId, Guid? viewerId);
        Task<(List<StoryHighlightArchiveCandidateModel> Items, int TotalItems)> GetArchiveCandidatesAsync(
            Guid ownerId,
            DateTime nowUtc,
            int page,
            int pageSize,
            Guid? excludeGroupId);
        Task<List<StoryHighlightArchiveCandidateModel>> GetArchiveStoriesByIdsForOwnerAsync(
            Guid ownerId,
            IReadOnlyCollection<Guid> storyIds,
            DateTime nowUtc);
        Task<HashSet<Guid>> GetExistingStoryIdsInGroupAsync(Guid groupId, IReadOnlyCollection<Guid> storyIds);
        Task AddGroupAsync(StoryHighlightGroup group);
        Task AddItemsAsync(IEnumerable<StoryHighlightItem> items);
        Task RemoveItemAsync(Guid groupId, Guid storyId);
        Task RemoveGroupAsync(StoryHighlightGroup group);
        Task UpdateGroupAsync(StoryHighlightGroup group);
    }
}
