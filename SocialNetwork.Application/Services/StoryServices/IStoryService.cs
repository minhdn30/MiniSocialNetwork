using SocialNetwork.Application.DTOs.StoryDTOs;

namespace SocialNetwork.Application.Services.StoryServices
{
    public interface IStoryService
    {
        Task<StoryDetailResponse> CreateStoryAsync(Guid currentId, StoryCreateRequest request);
        Task<StoryDetailResponse> UpdateStoryPrivacyAsync(Guid storyId, Guid currentId, StoryPrivacyUpdateRequest request);
        Task SoftDeleteStoryAsync(Guid storyId, Guid currentId);
    }
}
