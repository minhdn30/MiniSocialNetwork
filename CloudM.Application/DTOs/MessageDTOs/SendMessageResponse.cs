using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.MessageMediaDTOs;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.MessageDTOs
{
    public class SendMessageResponse
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public AccountChatInfoResponse Sender { get; set; } = null!;
        public string? Content { get; set; }
        public MessageTypeEnum MessageType { get; set; } // Text / Media / System
        public DateTime SentAt { get; set; }
        public bool IsEdited { get; set; }
        public bool IsRecalled { get; set; }
        public string? SystemMessageDataJson { get; set; }
        public string? TempId { get; set; }
        public List<MessageMediaResponse>? Medias { get; set; }
        public ReplyInfoModel? ReplyTo { get; set; }
        public StoryReplyInfoModel? StoryReplyInfo { get; set; }
    }
}
