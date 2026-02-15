using SocialNetwork.Domain.Entities;
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
    }
}
