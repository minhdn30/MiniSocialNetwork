using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;

namespace SocialNetwork.Infrastructure.Models
{
    public class PinnedMessageModel
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public string? Content { get; set; }
        public MessageTypeEnum MessageType { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRecalled { get; set; }
        public AccountChatInfoModel Sender { get; set; } = null!;
        public List<MessageMediaBasicModel>? Medias { get; set; }
        public AccountChatInfoModel PinnedByAccount { get; set; } = null!;
        public DateTime PinnedAt { get; set; }
    }
}
