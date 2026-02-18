using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Infrastructure.Models
{
    public class ReplyInfoModel
    {
        public Guid MessageId { get; set; }
        public string? Content { get; set; }
        public bool IsRecalled { get; set; }
        public MessageTypeEnum MessageType { get; set; }
        public Guid ReplySenderId { get; set; } // Internal: for nickname lookup
        public ReplySenderInfoModel? Sender { get; set; }
    }
}
