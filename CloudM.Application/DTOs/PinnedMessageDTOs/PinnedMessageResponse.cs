using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.MessageMediaDTOs;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
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
        public bool HasReply { get; set; }
        public ReplyInfoModel? ReplyTo { get; set; }
        public StoryReplyInfoModel? StoryReplyInfo { get; set; }
        public PostShareInfoModel? PostShareInfo { get; set; }

        // original message sender
        public AccountChatInfoResponse Sender { get; set; } = null!;

        // media (null if recalled)
        public List<MessageMediaResponse>? Medias { get; set; }

        // pin metadata
        public AccountChatInfoResponse PinnedByAccount { get; set; } = null!;
        public DateTime PinnedAt { get; set; }
    }
}
