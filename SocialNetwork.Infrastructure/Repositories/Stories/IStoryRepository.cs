using SocialNetwork.Domain.Entities;

namespace SocialNetwork.Infrastructure.Repositories.Stories
{
    public interface IStoryRepository
    {
        Task AddStoryAsync(Story story);
    }
}
