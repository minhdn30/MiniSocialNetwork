using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.ConversationMembers
{
    public class ConversationMemberRepository : IConversationMemberRepository
    {
        private readonly AppDbContext _context;
        public ConversationMemberRepository(AppDbContext context)
        {
            _context = context;
        }
        public Task AddConversationMember(ConversationMember member)
        {
            _context.ConversationMembers.Add(member);
            return Task.CompletedTask;
        }
        public Task AddConversationMembers(List<ConversationMember> members)
        {
            return _context.ConversationMembers.AddRangeAsync(members);
        }
        public Task UpdateConversationMember(ConversationMember member)
        {
            _context.ConversationMembers.Update(member);
            return Task.CompletedTask;
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

        public async Task<List<ConversationMember>> GetConversationMembersByAccountIdsAsync(Guid conversationId, IEnumerable<Guid> accountIds)
        {
            var normalizedIds = (accountIds ?? Enumerable.Empty<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
            {
                return new List<ConversationMember>();
            }

            return await _context.ConversationMembers
                .Where(cm => cm.ConversationId == conversationId && normalizedIds.Contains(cm.AccountId))
                .ToListAsync();
        }

        public async Task<List<Guid>> GetMemberIdsByConversationIdAsync(Guid conversationId)
        {
            return await _context.ConversationMembers
                .Where(cm => cm.ConversationId == conversationId && !cm.HasLeft)
                .Select(cm => cm.AccountId)
                .ToListAsync();
        }

        public async Task<List<ConversationMember>> GetConversationMembersAsync(Guid conversationId)
        {
            return await _context.ConversationMembers
                .Include(cm => cm.Account)
                .Where(cm => cm.ConversationId == conversationId && !cm.HasLeft)
                .ToListAsync();
        }

        public async Task<(List<ConversationMember> Members, int TotalCount)> GetConversationMembersPagedAsync(
            Guid conversationId,
            bool adminOnly,
            int page,
            int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = _context.ConversationMembers
                .AsNoTracking()
                .Include(cm => cm.Account)
                .Where(cm => cm.ConversationId == conversationId && !cm.HasLeft);

            if (adminOnly)
            {
                query = query.Where(cm => cm.IsAdmin);
            }

            var totalCount = await query.CountAsync();

            var members = await query
                .OrderByDescending(cm => cm.IsAdmin)
                .ThenBy(cm => cm.JoinedAt)
                .ThenBy(cm => cm.Account.Username)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (members, totalCount);
        }

        public async Task<Dictionary<Guid, bool>> GetMembersWithMuteStatusAsync(Guid conversationId)
        {
            return await _context.ConversationMembers
                .Where(cm => cm.ConversationId == conversationId && !cm.HasLeft)
                .ToDictionaryAsync(cm => cm.AccountId, cm => cm.IsMuted);
        }
    }
}
