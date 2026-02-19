using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using SocialNetwork.Infrastructure.Models;
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
        Task<ConversationResponse> CreateGroupConversationAsync(Guid currentId, CreateGroupConversationRequest request);
        Task<PagedResponse<ConversationListItemResponse>> GetConversationsPagedAsync(Guid currentId, bool? isPrivate, string? search, int page, int pageSize);
        Task<ConversationMessagesResponse> GetConversationMessagesWithMetaDataAsync(Guid conversationId, Guid currentId, string? cursor, int pageSize);
        Task<PrivateConversationIncludeMessagesResponse> GetPrivateConversationWithMessagesByOtherIdAsync(Guid currentId, Guid otherId, string? cursor, int pageSize);
        Task<int> GetUnreadConversationCountAsync(Guid currentId);
        Task<ConversationMessagesResponse> GetMessageContextAsync(Guid conversationId, Guid currentId, Guid messageId, int pageSize);
        Task<PagedResponse<MessageBasicModel>> SearchMessagesAsync(Guid conversationId, Guid currentId, string keyword, int page, int pageSize);
        Task<List<GroupInviteAccountSearchResponse>> SearchAccountsForGroupInviteAsync(Guid currentId, string keyword, IEnumerable<Guid>? excludeAccountIds, int limit = 10);
        Task<PagedResponse<ConversationMediaItemModel>> GetConversationMediaAsync(Guid conversationId, Guid currentId, int page, int pageSize);
        Task<PagedResponse<ConversationMediaItemModel>> GetConversationFilesAsync(Guid conversationId, Guid currentId, int page, int pageSize);
    }
}
