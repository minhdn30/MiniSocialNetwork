using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SocialNetwork.Infrastructure.Repositories.Conversations
{
    public class ConversationRepository : IConversationRepository
    {
        private readonly AppDbContext _context;
        public ConversationRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<Conversation?> GetConversationByTwoAccountIdsAsync(Guid accountId1, Guid accountId2)
        {
            return await _context.Conversations
                .Where(c => !c.IsDeleted && !c.IsGroup)
                .Where(c => c.Members.Count == 2 
                            && c.Members.Any(m => m.AccountId == accountId1) 
                            && c.Members.Any(m => m.AccountId == accountId2))
                .Include(c => c.Members)
                .FirstOrDefaultAsync();
        }
        public async Task AddConversationAsync(Conversation conversation)
        {
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
        } 
    }
}
