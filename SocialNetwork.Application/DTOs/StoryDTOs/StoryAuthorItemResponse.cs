using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Application.DTOs.StoryDTOs
{
    public class StoryAuthorItemResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public DateTime LatestStoryCreatedAt { get; set; }
        public int ActiveStoryCount { get; set; }
        public int UnseenCount { get; set; }
        public StoryRingStateEnum StoryRingState { get; set; } = StoryRingStateEnum.None;
        public bool IsCurrentUser { get; set; }
    }
}
