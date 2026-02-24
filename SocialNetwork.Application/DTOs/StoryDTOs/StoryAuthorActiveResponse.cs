namespace SocialNetwork.Application.DTOs.StoryDTOs
{
    public class StoryAuthorActiveResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public IReadOnlyList<StoryActiveItemResponse> Stories { get; set; } = Array.Empty<StoryActiveItemResponse>();
    }
}
