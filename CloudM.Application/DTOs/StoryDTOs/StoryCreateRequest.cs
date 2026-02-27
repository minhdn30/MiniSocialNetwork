using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CloudM.Application.DTOs.StoryDTOs
{
    public class StoryCreateRequest
    {
        public int? ContentType { get; set; }

        public IFormFile? MediaFile { get; set; }

        [MaxLength(1000)]
        public string? TextContent { get; set; }

        [MaxLength(100)]
        public string? BackgroundColorKey { get; set; }

        [MaxLength(100)]
        public string? FontTextKey { get; set; }

        [MaxLength(100)]
        public string? FontSizeKey { get; set; }

        [MaxLength(100)]
        public string? TextColorKey { get; set; }

        public int? Privacy { get; set; }

        public int? ExpiresEnum { get; set; }
    }
}
