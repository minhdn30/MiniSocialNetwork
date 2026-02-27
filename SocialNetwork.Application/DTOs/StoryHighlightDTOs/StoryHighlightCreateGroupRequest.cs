using Microsoft.AspNetCore.Http;

namespace SocialNetwork.Application.DTOs.StoryHighlightDTOs
{
    public class StoryHighlightCreateGroupRequest
    {
        public string? Name { get; set; }
        public IFormFile? CoverImageFile { get; set; }
        public List<Guid>? StoryIds { get; set; }
    }
}
