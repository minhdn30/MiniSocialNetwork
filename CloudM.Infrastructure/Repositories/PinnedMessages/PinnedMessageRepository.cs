using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.PinnedMessages
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

        public async Task<IEnumerable<PinnedMessageModel>> GetPinnedMessagesByConversationIdAsync(Guid conversationId, DateTime? clearedAt, Guid currentAccountId)
        {
            return await _context.PinnedMessages
                .AsNoTracking()
                .Where(pm => pm.ConversationId == conversationId)
                // filter ClearedAt (personal)
                .Where(pm => clearedAt == null || pm.Message.SentAt >= clearedAt)
                // filter HiddenBy (personal)
                .Where(pm => !pm.Message.HiddenBy.Any(hb => hb.AccountId == currentAccountId))
                // filter no recalled messages
                .Where(pm => !pm.Message.IsRecalled) 
                .OrderByDescending(pm => pm.PinnedAt)
                .Select(pm => new PinnedMessageModel
                {
                    MessageId = pm.MessageId,
                    ConversationId = pm.ConversationId,
                    Content = pm.Message.IsRecalled ? null : pm.Message.Content,
                    MessageType = pm.Message.MessageType,
                    SentAt = pm.Message.SentAt,
                    IsRecalled = pm.Message.IsRecalled,
                    PinnedAt = pm.PinnedAt,
                    Sender = new AccountChatInfoModel
                    {
                        AccountId = pm.Message.Account.AccountId,
                        Username = pm.Message.Account.Username,
                        FullName = pm.Message.Account.FullName,
                        AvatarUrl = pm.Message.Account.AvatarUrl,
                        IsActive = pm.Message.Account.Status == AccountStatusEnum.Active
                    },
                    PinnedByAccount = new AccountChatInfoModel
                    {
                        AccountId = pm.PinnedByAccount.AccountId,
                        Username = pm.PinnedByAccount.Username,
                        FullName = pm.PinnedByAccount.FullName,
                        AvatarUrl = pm.PinnedByAccount.AvatarUrl,
                        IsActive = pm.PinnedByAccount.Status == AccountStatusEnum.Active
                    },
                    Medias = pm.Message.IsRecalled ? null : pm.Message.Medias
                        .OrderBy(m => m.CreatedAt)
                        .Select(m => new MessageMediaBasicModel
                        {
                            MessageMediaId = m.MessageMediaId,
                            MediaUrl = m.MediaUrl,
                            ThumbnailUrl = m.ThumbnailUrl,
                            MediaType = m.MediaType,
                            FileName = m.FileName,
                            FileSize = m.FileSize,
                            CreatedAt = m.CreatedAt
                        }).ToList()
                })
                .ToListAsync();
        }
    }
}
