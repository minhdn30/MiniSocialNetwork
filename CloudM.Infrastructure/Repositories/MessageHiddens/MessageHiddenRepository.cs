using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.MessageHiddens
{
    public class MessageHiddenRepository : IMessageHiddenRepository
    {
        private readonly AppDbContext _context;

        public MessageHiddenRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsMessageHiddenByAccountAsync(Guid messageId, Guid accountId)
        {
            return await _context.MessageHiddens
                .AnyAsync(mh => mh.MessageId == messageId && mh.AccountId == accountId);
        }

        public Task HideMessageAsync(MessageHidden messageHidden)
        {
            _context.MessageHiddens.Add(messageHidden);
            return Task.CompletedTask;
        }

        public async Task UnhideMessageAsync(Guid messageId, Guid accountId)
        {
            var hidden = await _context.MessageHiddens
                .FirstOrDefaultAsync(mh => mh.MessageId == messageId && mh.AccountId == accountId);
            
            if (hidden != null)
            {
                _context.MessageHiddens.Remove(hidden);
            }
        }
    }
}
