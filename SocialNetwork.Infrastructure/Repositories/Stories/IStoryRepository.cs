using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Models;

namespace SocialNetwork.Infrastructure.Repositories.Stories
{
    public interface IStoryRepository
    {
        Task AddStoryAsync(Story story);
        Task<Story?> GetStoryByIdAsync(Guid storyId);
        Task UpdateStoryAsync(Story story);
        Task<List<StoryRingStatsByAuthorModel>> GetStoryRingStatsByAuthorAsync(
            Guid currentId,
            IReadOnlyCollection<Guid> authorIds,
            DateTime nowUtc);
    }
}
