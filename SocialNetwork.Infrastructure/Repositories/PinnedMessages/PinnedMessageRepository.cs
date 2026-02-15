using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.PinnedMessages
{
    public class PinnedMessageRepository : IPinnedMessageRepository
    {
        private readonly AppDbContext _context;

        public PinnedMessageRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsPinnedAsync(Guid conversationId, Guid messageId)
        {
            return await _context.PinnedMessages
                .AnyAsync(pm => pm.ConversationId == conversationId && pm.MessageId == messageId);
        }

        public Task AddAsync(PinnedMessage pinnedMessage)
        {
            _context.PinnedMessages.Add(pinnedMessage);
            return Task.CompletedTask;
        }

        public async Task RemoveAsync(Guid conversationId, Guid messageId)
        {
            var pinned = await _context.PinnedMessages
                .FirstOrDefaultAsync(pm => pm.ConversationId == conversationId && pm.MessageId == messageId);

            if (pinned != null)
            {
                _context.PinnedMessages.Remove(pinned);
            }
        }
    }
}
