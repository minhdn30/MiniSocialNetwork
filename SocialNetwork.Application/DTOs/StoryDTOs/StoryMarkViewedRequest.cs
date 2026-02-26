namespace SocialNetwork.Application.DTOs.StoryDTOs
{
    public class StoryMarkViewedRequest
    {
        public List<Guid> StoryIds { get; set; } = new();
    }
}
