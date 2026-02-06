using System.ComponentModel.DataAnnotations;

namespace SocialNetwork.Application.DTOs.PostDTOs
{
    public class PostUpdateContentRequest
    {
        [MaxLength(3000)]
        public string? Content { get; set; }
        public int? Privacy { get; set; }
    }
}
