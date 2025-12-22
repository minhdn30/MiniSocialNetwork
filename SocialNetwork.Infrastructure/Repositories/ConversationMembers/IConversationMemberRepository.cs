using SocialNetwork.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.ConversationMembers
{
    public interface IConversationMemberRepository
    {
        Task AddConversationMember(ConversationMember member);
        Task AddConversationMembers(List<ConversationMember> members);
    }
}
