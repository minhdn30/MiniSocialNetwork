using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.StoryDTOs;

namespace SocialNetwork.Application.Services.StoryServices
{
    public interface IStoryService
    {
        Task<StoryDetailResponse> CreateStoryAsync(Guid currentId, StoryCreateRequest request);
        Task<StoryDetailResponse> UpdateStoryPrivacyAsync(Guid storyId, Guid currentId, StoryPrivacyUpdateRequest request);
        Task SoftDeleteStoryAsync(Guid storyId, Guid currentId);
        Task<PagedResponse<StoryAuthorItemResponse>> GetViewableAuthorsAsync(Guid currentId, int page, int pageSize);
        Task<StoryAuthorActiveResponse> GetActiveStoriesByAuthorAsync(Guid currentId, Guid authorId);
        Task<StoryResolveResponse?> ResolveStoryAsync(Guid currentId, Guid storyId);
    }
}
