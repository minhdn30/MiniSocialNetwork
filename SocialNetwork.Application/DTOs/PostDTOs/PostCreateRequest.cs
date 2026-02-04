using Microsoft.AspNetCore.Http;
using SocialNetwork.Application.DTOs.PostMediaDTOs;
using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.PostDTOs
{
    public class PostCreateRequest
    {
        [MaxLength(3000)]
        public string? Content { get; set; }
        //use int? for optional enum, because FromForm cannot bind nullable enum directly
        public int? Privacy { get; set; }
        public int? FeedAspectRatio { get; set; }
        [Required(ErrorMessage = "At least one image is required.")]
        [MinLength(1, ErrorMessage = "At least one image is required.")]
        public List<IFormFile> MediaFiles { get; set; } = new();
        // JSON string describing the crop for each image
        // Example: [[{ "index":0, "cropX":0.1, ... }]]
        public string? MediaCrops { get; set; }
    }
}
