using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Messages
{
    public class MessageRepository : IMessageRepository
    {
        private readonly AppDbContext _context;
        public MessageRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<(IEnumerable<MessageBasicModel> msg, int TotalItems)> GetMessagesByConversationId(Guid conversationId, Guid currentId, int page, int pageSize)
        {
            var member = await _context.ConversationMembers
                .FirstOrDefaultAsync(cm => cm.ConversationId == conversationId &&
                            cm.AccountId == currentId &&
                            !cm.HasLeft);
            var clearedAt = member?.ClearedAt;
            var query = _context.Messages
                .Where(m => m.ConversationId == conversationId && 
                       (clearedAt == null || m.SentAt > clearedAt) &&
                       m.Account.Status == AccountStatusEnum.Active)
                .OrderByDescending(m => m.SentAt);
            var totalItems = await query.CountAsync();
            var messages = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MessageBasicModel
                {
                    MessageId = m.MessageId,
                    Content = m.Content,
                    MessageType = m.MessageType,
                    SentAt = m.SentAt,
                    IsEdited = m.IsEdited,
                    IsDeleted = m.IsDeleted,

                    Sender = new AccountBasicInfoModel
                    {
                        AccountId = m.Account.AccountId,
                        Username = m.Account.Username,
                        FullName = m.Account.FullName,
                        AvatarUrl = m.Account.AvatarUrl,
                        Status = m.Account.Status
                    },

                    Medias = m.Medias
                .OrderBy(mm => mm.CreatedAt)
                .Select(mm => new MessageMediaBasicModel
                {
                    MessageMediaId = mm.MessageMediaId,
                    MediaUrl = mm.MediaUrl,
                    ThumbnailUrl = mm.ThumbnailUrl,
                    MediaType = mm.MediaType,
                    FileName = mm.FileName,
                    FileSize = mm.FileSize,
                    CreatedAt = mm.CreatedAt,
                })
                .ToList()
                })
                .ToListAsync();
            return (messages, totalItems);
        }
        public async Task AddMessageAsync(Message message)
        {
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
        }
        public async Task<bool> IsMessageNewer(Guid newMessageId, Guid? lastSeenMessageId)
        {
            if (lastSeenMessageId == null)
                return true;

            var times = await _context.Messages
                .Where(m => m.MessageId == newMessageId || m.MessageId == lastSeenMessageId)
                .Select(m => new { m.MessageId, m.SentAt })
                .ToListAsync();

            if (times.Count < 2)
                return false;

            var newTime = times.First(m => m.MessageId == newMessageId).SentAt;
            var oldTime = times.First(m => m.MessageId == lastSeenMessageId).SentAt;

            return newTime > oldTime;
        }
        public async Task<int> CountUnreadMessagesAsync(Guid conversationId, Guid currentId, DateTime? lastSeenAt)
        {
            var query = _context.Messages
                .AsNoTracking()
                .Where(m =>
                    m.ConversationId == conversationId &&
                    m.AccountId != currentId);
            if (lastSeenAt.HasValue)
            {
                query = query.Where(m => m.SentAt > lastSeenAt.Value);
            }
            return await query.CountAsync();
        }


    }
}
