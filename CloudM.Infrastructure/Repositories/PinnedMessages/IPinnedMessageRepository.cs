using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.PinnedMessages
{
    public interface IPinnedMessageRepository
    {
        Task<bool> IsPinnedAsync(Guid conversationId, Guid messageId);
        Task AddAsync(PinnedMessage pinnedMessage);
        Task RemoveAsync(Guid conversationId, Guid messageId);
        Task<IEnumerable<PinnedMessageModel>> GetPinnedMessagesByConversationIdAsync(Guid conversationId, DateTime? clearedAt, Guid currentAccountId);
    }
}
