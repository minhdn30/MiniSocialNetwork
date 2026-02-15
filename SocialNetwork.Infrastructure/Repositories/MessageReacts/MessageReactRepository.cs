using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.MessageReacts
{
    public class MessageReactRepository : IMessageReactRepository
    {
        private readonly AppDbContext _context;

        public MessageReactRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<MessageReact?> GetReactAsync(Guid messageId, Guid accountId)
        {
            return await _context.MessageReacts
                .FirstOrDefaultAsync(mr => mr.MessageId == messageId && mr.AccountId == accountId);
        }

        public async Task<IEnumerable<MessageReact>> GetReactsByMessageIdAsync(Guid messageId)
        {
            return await _context.MessageReacts
                .Where(mr => mr.MessageId == messageId)
                .Include(mr => mr.Account)
                .Where(mr => mr.Account.Status == AccountStatusEnum.Active)
                .ToListAsync();
        }

        public async Task<int> CountReactsByMessageIdAsync(Guid messageId)
        {
            return await _context.MessageReacts
                .Where(mr => mr.MessageId == messageId)
                .Where(mr => mr.Account.Status == AccountStatusEnum.Active)
                .CountAsync();
        }

        public async Task<int> CountReactsByMessageIdAndTypeAsync(Guid messageId, ReactEnum reactType)
        {
            return await _context.MessageReacts
                .Where(mr => mr.MessageId == messageId && mr.ReactType == reactType)
                .Where(mr => mr.Account.Status == AccountStatusEnum.Active)
                .CountAsync();
        }

        public Task AddReactAsync(MessageReact react)
        {
            _context.MessageReacts.Add(react);
            return Task.CompletedTask;
        }

        public async Task RemoveReactAsync(Guid messageId, Guid accountId)
        {
            var react = await _context.MessageReacts
                .FirstOrDefaultAsync(mr => mr.MessageId == messageId && mr.AccountId == accountId);
            
            if (react != null)
            {
                _context.MessageReacts.Remove(react);
            }
        }

        public Task UpdateReactAsync(MessageReact react)
        {
            _context.MessageReacts.Update(react);
            return Task.CompletedTask;
        }
    }
}
