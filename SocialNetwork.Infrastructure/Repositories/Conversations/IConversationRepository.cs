using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Conversations
{
    public interface IConversationRepository
    {
        Task<Conversation?> GetConversationByTwoAccountIdsAsync(Guid accountId1, Guid accountId2);
        Task AddConversationAsync(Conversation conversation);
        Task<bool> IsPrivateConversationExistBetweenTwoAccounts(Guid accountId1, Guid accountId2);
        Task<Conversation> CreatePrivateConversationAsync(Guid currentId, Guid otherId);
        Task<(List<ConversationListModel> Items, int TotalCount)> GetConversationsPagedAsync(Guid currentId, bool? isPrivate, string? search, int page, int pageSize);
        Task<ConversationListModel?> GetConversationMetaDataAsync(Guid conversationId, Guid currentId);
        Task<Guid?> GetPrivateConversationIdAsync(Guid accountId1, Guid accountId2);
        Task<Conversation?> GetConversationByIdAsync(Guid conversationId);
    }
}
