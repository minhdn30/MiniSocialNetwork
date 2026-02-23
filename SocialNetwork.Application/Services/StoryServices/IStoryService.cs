using SocialNetwork.Application.DTOs.StoryDTOs;
using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Application.Services.StoryServices
{
    public interface IStoryService
    {
        Task<StoryDetailResponse> CreateStoryAsync(Guid currentId, StoryCreateRequest request);
        Task<StoryDetailResponse> UpdateStoryPrivacyAsync(Guid storyId, Guid currentId, StoryPrivacyUpdateRequest request);
        Task SoftDeleteStoryAsync(Guid storyId, Guid currentId);
        Task<IReadOnlyDictionary<Guid, StoryRingStateEnum>> GetStoryRingStatesForAuthorsAsync(Guid currentId, IEnumerable<Guid> authorIds);
    }
}
