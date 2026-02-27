using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.StoryHighlightDTOs;

namespace SocialNetwork.Application.Services.StoryHighlightServices
{
    public interface IStoryHighlightService
    {
        Task<List<StoryHighlightGroupListItemResponse>> GetProfileHighlightGroupsAsync(Guid targetAccountId, Guid? currentId);
        Task<StoryHighlightGroupStoriesResponse> GetHighlightGroupStoriesAsync(Guid targetAccountId, Guid groupId, Guid? currentId);
        Task<PagedResponse<StoryHighlightArchiveCandidateResponse>> GetArchiveCandidatesAsync(Guid currentId, int page, int pageSize, Guid? excludeGroupId);
        Task<StoryHighlightGroupMutationResponse> CreateGroupAsync(Guid currentId, StoryHighlightCreateGroupRequest request);
        Task<StoryHighlightGroupMutationResponse> AddItemsAsync(Guid currentId, Guid groupId, StoryHighlightAddItemsRequest request);
        Task<StoryHighlightGroupMutationResponse> UpdateGroupAsync(Guid currentId, Guid groupId, StoryHighlightUpdateGroupRequest request);
        Task RemoveItemAsync(Guid currentId, Guid groupId, Guid storyId);
        Task DeleteGroupAsync(Guid currentId, Guid groupId);
    }
}
