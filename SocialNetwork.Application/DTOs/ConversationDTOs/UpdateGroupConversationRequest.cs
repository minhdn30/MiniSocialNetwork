using Microsoft.AspNetCore.Http;

namespace SocialNetwork.Application.DTOs.ConversationDTOs
{
    public class UpdateGroupConversationRequest
    {
        public string? ConversationName { get; set; }
        public IFormFile? ConversationAvatar { get; set; }
        public bool RemoveAvatar { get; set; }
    }
}
