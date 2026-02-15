using SocialNetwork.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.MessageHiddens
{
    public interface IMessageHiddenRepository
    {
        Task<bool> IsMessageHiddenByAccountAsync(Guid messageId, Guid accountId);
        Task HideMessageAsync(MessageHidden messageHidden);
        Task UnhideMessageAsync(Guid messageId, Guid accountId);
    }
}
