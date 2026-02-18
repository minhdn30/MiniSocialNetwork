using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.MessageServices
{
    public interface IMessageService
    {
        Task<CursorResponse<MessageBasicModel>> GetMessagesByConversationIdAsync(Guid conversationId, Guid currentId, string? cursor, int pageSize);
        Task<SendMessageResponse> SendMessageInPrivateChatAsync(Guid senderId, SendMessageInPrivateChatRequest request);
        Task<SendMessageResponse> SendMessageInGroupAsync(Guid senderId, Guid conversationId, SendMessageRequest request);
        Task<string> GetMediaDownloadUrlAsync(Guid messageMediaId, Guid accountId);
        Task<RecallMessageResponse> RecallMessageAsync(Guid messageId, Guid accountId);
    }
}
