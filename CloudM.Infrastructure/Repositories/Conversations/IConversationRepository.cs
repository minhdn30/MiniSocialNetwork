using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.Conversations
{
    public interface IConversationRepository
    {
        Task<Conversation?> GetConversationByTwoAccountIdsAsync(Guid accountId1, Guid accountId2);
        Task AddConversationAsync(Conversation conversation);
        Task<bool> IsPrivateConversationExistBetweenTwoAccounts(Guid accountId1, Guid accountId2);
        Task<Conversation> CreatePrivateConversationAsync(Guid currentId, Guid otherId);
        Task<(List<ConversationListModel> Items, bool HasMore)> GetConversationsByCursorAsync(
            Guid currentId,
            bool? isPrivate,
            string? search,
            DateTime? cursorLastMessageSentAt,
            Guid? cursorConversationId,
            int limit);
        Task<ConversationListModel?> GetConversationMetaDataAsync(Guid conversationId, Guid currentId);
        Task<Guid?> GetPrivateConversationIdAsync(Guid accountId1, Guid accountId2);
        Task<Conversation?> GetConversationByIdAsync(Guid conversationId);
        Task UpdateConversationAsync(Conversation conversation);
        Task<int> GetUnreadConversationCountAsync(Guid currentId);
        Task<List<PostShareGroupConversationSearchModel>> SearchGroupConversationsForPostShareAsync(Guid currentId, string keyword, int limit = 20);
    }
}
