using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
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
                            && c.Members.Any(m => m.AccountId == accountId1 && m.Account.Status == AccountStatusEnum.Active) 
                            && c.Members.Any(m => m.AccountId == accountId2 && m.Account.Status == AccountStatusEnum.Active))
                .Include(c => c.Members)
                    .ThenInclude(m => m.Account)
                .FirstOrDefaultAsync();
        }
        public async Task AddConversationAsync(Conversation conversation)
        {
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
        } 
        public async Task<bool> IsPrivateConversationExistBetweenTwoAccounts(Guid accountId1, Guid accountId2)
        {
            return await _context.Conversations.AnyAsync(c =>
    !           c.IsDeleted &&
                !c.IsGroup &&
                c.Members.Count == 2 &&
                c.Members.Any(m => m.AccountId == accountId1 && m.Account.Status == AccountStatusEnum.Active) &&
                c.Members.Any(m => m.AccountId == accountId2 && m.Account.Status == AccountStatusEnum.Active)
            );
        }
        public async Task<Conversation> CreatePrivateConversationAsync(Guid currentId, Guid otherId)
        {
            var conversation = new Conversation
            {
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentId
            };
            _context.Conversations.Add(conversation);
            var members = new List<ConversationMember>
                {
                    new ConversationMember
                    {
                        ConversationId = conversation.ConversationId,
                        AccountId = currentId
                    },
                    new ConversationMember
                    {
                        ConversationId = conversation.ConversationId,
                        AccountId = otherId
                    }
                };
            _context.ConversationMembers.AddRange(members);
            await _context.SaveChangesAsync();
            return conversation;
        }
    }
}
