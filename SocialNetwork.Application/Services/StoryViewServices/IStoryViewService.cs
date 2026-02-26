using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.StoryDTOs;
using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Application.Services.StoryViewServices
{
    public interface IStoryViewService
    {
        Task<PagedResponse<StoryAuthorItemResponse>> GetViewableAuthorsAsync(Guid currentId, int page, int pageSize);
        Task<StoryAuthorActiveResponse> GetActiveStoriesByAuthorAsync(Guid currentId, Guid authorId);
        Task<StoryMarkViewedResponse> MarkStoriesViewedAsync(Guid currentId, StoryMarkViewedRequest request);
        Task<IReadOnlyDictionary<Guid, StoryRingStateEnum>> GetStoryRingStatesForAuthorsAsync(Guid currentId, IEnumerable<Guid> authorIds);
        Task<StoryActiveItemResponse> ReactStoryAsync(Guid currentId, Guid storyId, StoryReactRequest request);
        Task<PagedResponse<StoryViewerBasicResponse>> GetStoryViewersAsync(Guid currentId, Guid storyId, int page, int pageSize);
    }
}
