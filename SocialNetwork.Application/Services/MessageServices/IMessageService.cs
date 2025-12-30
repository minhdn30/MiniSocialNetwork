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
        Task<PagedResponse<MessageBasicModel>> GetMessagesByConversationIdAsync(Guid conversationId, Guid currentId, int page, int pageSize);
        Task<SendMessageResponse> SendMessageInPrivateChatAsync(Guid senderId, SendMessageInPrivateChatRequest request);
    }
}
