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
        Task UpdateConversationMember(ConversationMember member);
        Task<bool> IsMemberOfConversation(Guid conversationId, Guid accountId);
        Task<ConversationMember?> GetConversationMemberAsync(Guid conversationId, Guid accountId);
        Task<List<Guid>> GetMemberIdsByConversationIdAsync(Guid conversationId);
    }
}
