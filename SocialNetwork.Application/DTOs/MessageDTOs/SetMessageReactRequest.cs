using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Application.DTOs.MessageDTOs
{
    public class SetMessageReactRequest
    {
        public ReactEnum ReactType { get; set; }
    }
}
