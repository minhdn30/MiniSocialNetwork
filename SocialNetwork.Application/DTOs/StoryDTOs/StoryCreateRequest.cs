using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SocialNetwork.Application.DTOs.StoryDTOs
{
    public class StoryCreateRequest
    {
        public int? ContentType { get; set; }

        public IFormFile? MediaFile { get; set; }

        [MaxLength(2000)]
        public string? ThumbnailUrl { get; set; }

        [MaxLength(1000)]
        public string? TextContent { get; set; }

        public int? Privacy { get; set; }

        public int? ExpiresEnum { get; set; }
    }
}
