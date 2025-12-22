using SocialNetwork.Domain.Entities;
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

    }
}
