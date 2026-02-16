using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.PinnedMessages
{
    public interface IPinnedMessageRepository
    {
        Task<bool> IsPinnedAsync(Guid conversationId, Guid messageId);
        Task AddAsync(PinnedMessage pinnedMessage);
        Task RemoveAsync(Guid conversationId, Guid messageId);
        Task<IEnumerable<PinnedMessageModel>> GetPinnedMessagesByConversationIdAsync(Guid conversationId, DateTime? clearedAt, Guid currentAccountId);
    }
}
