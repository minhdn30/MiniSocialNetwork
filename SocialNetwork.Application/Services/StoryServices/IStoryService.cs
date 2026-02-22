using SocialNetwork.Application.DTOs.StoryDTOs;

namespace SocialNetwork.Application.Services.StoryServices
{
    public interface IStoryService
    {
        Task<StoryDetailResponse> CreateStoryAsync(Guid currentId, StoryCreateRequest request);
    }
}
