using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.ConversationMembers
{
    public class ConversationMemberRepository : IConversationMemberRepository
    {
        private readonly AppDbContext _context;
        public ConversationMemberRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task AddConversationMember(ConversationMember member)
        {
            _context.ConversationMembers.Add(member);
            await _context.SaveChangesAsync();
        }
        public async Task AddConversationMembers(List<ConversationMember> members)
        {
            await _context.ConversationMembers.AddRangeAsync(members);
            await _context.SaveChangesAsync();
        }
        public async Task UpdateConversationMember(ConversationMember member)
        {
            _context.ConversationMembers.Update(member);
            await _context.SaveChangesAsync();
        }
        public async Task<bool> IsMemberOfConversation(Guid conversationId, Guid accountId)
        {
            return await _context.ConversationMembers
                .AnyAsync(cm => cm.ConversationId == conversationId
                                && cm.AccountId == accountId
                                && !cm.HasLeft);
        }
        public async Task<ConversationMember?> GetConversationMemberAsync(Guid conversationId, Guid accountId)
        {
            return await _context.ConversationMembers
                .FirstOrDefaultAsync(cm => cm.ConversationId == conversationId
                                           && cm.AccountId == accountId 
                                           && !cm.HasLeft);
        }
    }
}
