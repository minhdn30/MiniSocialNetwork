using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Infrastructure.Repositories.Stories
{
    public interface IStoryRepository
    {
        Task AddStoryAsync(Story story);
        Task<Story?> GetStoryByIdAsync(Guid storyId);
        Task UpdateStoryAsync(Story story);
        Task<bool> HasRecentStoryAsync(Guid accountId, StoryContentTypeEnum contentType, TimeSpan window);
        Task<bool> ExistsAndActiveAsync(Guid storyId);
        Task<HashSet<Guid>> GetActiveStoryIdsAsync(IEnumerable<Guid> storyIds);
    }
}
