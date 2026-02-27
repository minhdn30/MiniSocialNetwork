using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;

namespace CloudM.Application.DTOs.MessageDTOs
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
