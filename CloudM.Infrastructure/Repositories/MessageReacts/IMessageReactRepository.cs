using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.MessageReacts
{
    public interface IMessageReactRepository
    {
        Task<MessageReact?> GetReactAsync(Guid messageId, Guid accountId);
        Task<IEnumerable<MessageReact>> GetReactsByMessageIdAsync(Guid messageId);
        Task<int> CountReactsByMessageIdAsync(Guid messageId);
        Task<int> CountReactsByMessageIdAndTypeAsync(Guid messageId, ReactEnum reactType);
        Task AddReactAsync(MessageReact react);
        Task RemoveReactAsync(Guid messageId, Guid accountId);
        Task UpdateReactAsync(MessageReact react);
    }
}
