using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;

namespace SocialNetwork.Application.DTOs.MessageDTOs
{
    public class MessageReactStateResponse
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public bool IsReacted { get; set; }
        public ReactEnum? CurrentUserReactType { get; set; }
        public int TotalReacts { get; set; }
        public List<MessageReactSummaryModel> Reacts { get; set; } = new();
        public List<MessageReactAccountModel> ReactedBy { get; set; } = new();
    }
}
