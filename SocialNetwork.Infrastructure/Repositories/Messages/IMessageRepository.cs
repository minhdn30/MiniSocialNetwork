using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Messages
{
    public interface IMessageRepository
    {
        Task<(IEnumerable<MessageBasicModel> msg, int TotalItems)> GetMessagesByConversationId(Guid conversationId, Guid currentId, int page, int pageSize);
    }
}
