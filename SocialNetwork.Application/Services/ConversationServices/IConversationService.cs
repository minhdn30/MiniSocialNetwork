using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.ConversationServices
{
    public interface IConversationService
    {
        Task<ConversationResponse?> GetPrivateConversationAsync(Guid currentId, Guid otherId);
        Task<ConversationResponse> CreatePrivateConversationAsync(Guid currentId, Guid otherId);
        Task<PagedResponse<ConversationListItemResponse>> GetConversationsPagedAsync(Guid currentId, bool? isPrivate, string? search, int page, int pageSize);
        Task<ConversationMessagesResponse> GetConversationMessagesWithMetaDataAsync(Guid conversationId, Guid currentId, int page, int pageSize);
        Task<PrivateConversationIncludeMessagesResponse> GetPrivateConversationWithMessagesByOtherIdAsync(Guid currentId, Guid otherId, int page, int pageSize);
        Task<int> GetUnreadConversationCountAsync(Guid currentId);
    }
}
