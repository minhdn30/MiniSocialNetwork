using Microsoft.AspNetCore.Http;

namespace CloudM.Application.DTOs.StoryHighlightDTOs
{
    public class StoryHighlightUpdateGroupRequest
    {
        public string? Name { get; set; }
        public IFormFile? CoverImageFile { get; set; }
        public bool? RemoveCoverImage { get; set; }
    }
}
