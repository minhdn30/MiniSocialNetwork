using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.StoryDTOs;
using CloudM.Domain.Enums;

namespace CloudM.Application.Services.StoryViewServices
{
    public interface IStoryViewService
    {
        Task<StoryMarkViewedResponse> MarkStoriesViewedAsync(Guid currentId, StoryMarkViewedRequest request);
        Task<IReadOnlyDictionary<Guid, StoryRingStateEnum>> GetStoryRingStatesForAuthorsAsync(Guid currentId, IEnumerable<Guid> authorIds);
        Task<StoryActiveItemResponse> ReactStoryAsync(Guid currentId, Guid storyId, StoryReactRequest request);
        Task<PagedResponse<StoryViewerBasicResponse>> GetStoryViewersAsync(Guid currentId, Guid storyId, int page, int pageSize);
    }
}
