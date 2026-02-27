using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.MessageMediaDTOs;
using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;

namespace CloudM.Application.DTOs.PinnedMessageDTOs
{
    public class PinnedMessageResponse
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public string? Content { get; set; }
        public MessageTypeEnum MessageType { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRecalled { get; set; }

        // Original message sender
        public AccountChatInfoResponse Sender { get; set; } = null!;

        // Media (null if recalled)
        public List<MessageMediaResponse>? Medias { get; set; }

        // Pin metadata
        public AccountChatInfoResponse PinnedByAccount { get; set; } = null!;
        public DateTime PinnedAt { get; set; }
    }
}
