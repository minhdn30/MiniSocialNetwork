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
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.ConversationId == conversationId &&
                            cm.AccountId == currentId &&
                            !cm.HasLeft);
            var clearedAt = member?.ClearedAt;
            var query = _context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId && 
                       (clearedAt == null || m.SentAt >= clearedAt) &&
                       m.Account.Status == AccountStatusEnum.Active &&
                       !m.HiddenBy.Any(hb => hb.AccountId == currentId))
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
                    IsRecalled = m.IsRecalled,
                    SystemMessageDataJson = m.SystemMessageDataJson,
                    IsPinned = _context.PinnedMessages.Any(pm => pm.ConversationId == conversationId && pm.MessageId == m.MessageId),

                    Sender = new AccountChatInfoModel
                    {
                        AccountId = m.Account.AccountId,
                        Username = m.Account.Username,
                        FullName = m.Account.FullName,
                        AvatarUrl = m.Account.AvatarUrl,
                        IsActive = m.Account.Status == AccountStatusEnum.Active
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
        public Task AddMessageAsync(Message message)
        {
            _context.Messages.Add(message);
            return Task.CompletedTask;
        }
        public async Task<bool> IsMessageNewer(Guid newMessageId, Guid? lastSeenMessageId)
        {
            if (lastSeenMessageId == null)
                return true;

            var times = await _context.Messages
                .AsNoTracking()
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
            return await _context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId &&
                            m.AccountId != currentId &&
                            !m.HiddenBy.Any(hb => hb.AccountId == currentId))
                // Combine with member's ClearedAt in a single query
                .Where(m => _context.ConversationMembers
                    .Any(cm => cm.ConversationId == conversationId && 
                               cm.AccountId == currentId && 
                               (cm.ClearedAt == null || m.SentAt >= cm.ClearedAt.Value)))
                .Where(m => !lastSeenAt.HasValue || m.SentAt > lastSeenAt.Value)
                .CountAsync();
        }


        public async Task<Message?> GetMessageByIdAsync(Guid messageId)
        {
            return await _context.Messages.FirstOrDefaultAsync(m => m.MessageId == messageId);
        }

        public async Task<int> GetMessagePositionAsync(Guid conversationId, Guid currentId, Guid messageId)
        {
            var member = await _context.ConversationMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.ConversationId == conversationId &&
                            cm.AccountId == currentId &&
                            !cm.HasLeft);
            var clearedAt = member?.ClearedAt;

            var targetMessage = await _context.Messages
                .AsNoTracking()
                .Where(m => m.MessageId == messageId && m.ConversationId == conversationId)
                .Select(m => new { m.SentAt })
                .FirstOrDefaultAsync();

            if (targetMessage == null) return -1;

            // Count messages with SentAt >= target (same ORDER BY DESC logic as main query)
            // Position 1 = newest message, position N = oldest
            var position = await _context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId &&
                       (clearedAt == null || m.SentAt >= clearedAt) &&
                       m.Account.Status == AccountStatusEnum.Active &&
                       !m.HiddenBy.Any(hb => hb.AccountId == currentId))
                .Where(m => m.SentAt >= targetMessage.SentAt)
                .CountAsync();

            return position;
        }

        public async Task<(IEnumerable<ConversationMediaItemModel> items, int totalItems)> GetConversationMediaAsync(Guid conversationId, Guid currentId, int page, int pageSize)
        {
            var member = await _context.ConversationMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.ConversationId == conversationId &&
                            cm.AccountId == currentId &&
                            !cm.HasLeft);
            var clearedAt = member?.ClearedAt;

            var query = _context.MessageMedias
                .AsNoTracking()
                .Where(mm => mm.Message.ConversationId == conversationId &&
                            (clearedAt == null || mm.Message.SentAt >= clearedAt) &&
                            mm.Message.Account.Status == AccountStatusEnum.Active &&
                            !mm.Message.IsRecalled &&
                            !mm.Message.HiddenBy.Any(hb => hb.AccountId == currentId) &&
                            (mm.MediaType == MediaTypeEnum.Image || mm.MediaType == MediaTypeEnum.Video))
                .OrderByDescending(mm => mm.Message.SentAt)
                .ThenByDescending(mm => mm.CreatedAt)
                .ThenByDescending(mm => mm.MessageMediaId);

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(mm => new ConversationMediaItemModel
                {
                    MessageId = mm.MessageId,
                    MessageMediaId = mm.MessageMediaId,
                    MediaUrl = mm.MediaUrl,
                    ThumbnailUrl = mm.ThumbnailUrl,
                    MediaType = mm.MediaType,
                    FileName = mm.FileName,
                    FileSize = mm.FileSize,
                    SentAt = mm.Message.SentAt,
                    CreatedAt = mm.CreatedAt
                })
                .ToListAsync();

            return (items, totalItems);
        }

        public async Task<(IEnumerable<ConversationMediaItemModel> items, int totalItems)> GetConversationFilesAsync(Guid conversationId, Guid currentId, int page, int pageSize)
        {
            var member = await _context.ConversationMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.ConversationId == conversationId &&
                            cm.AccountId == currentId &&
                            !cm.HasLeft);
            var clearedAt = member?.ClearedAt;

            var query = _context.MessageMedias
                .AsNoTracking()
                .Where(mm => mm.Message.ConversationId == conversationId &&
                            (clearedAt == null || mm.Message.SentAt >= clearedAt) &&
                            mm.Message.Account.Status == AccountStatusEnum.Active &&
                            !mm.Message.IsRecalled &&
                            !mm.Message.HiddenBy.Any(hb => hb.AccountId == currentId) &&
                            mm.MediaType == MediaTypeEnum.Document)
                .OrderByDescending(mm => mm.Message.SentAt)
                .ThenByDescending(mm => mm.CreatedAt)
                .ThenByDescending(mm => mm.MessageMediaId);

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(mm => new ConversationMediaItemModel
                {
                    MessageId = mm.MessageId,
                    MessageMediaId = mm.MessageMediaId,
                    MediaUrl = mm.MediaUrl,
                    ThumbnailUrl = mm.ThumbnailUrl,
                    MediaType = mm.MediaType,
                    FileName = mm.FileName,
                    FileSize = mm.FileSize,
                    SentAt = mm.Message.SentAt,
                    CreatedAt = mm.CreatedAt
                })
                .ToListAsync();

            return (items, totalItems);
        }

        public async Task<(IEnumerable<MessageBasicModel> items, int totalItems)> SearchMessagesAsync(Guid conversationId, Guid currentId, string keyword, int page, int pageSize)
        {
            var member = await _context.ConversationMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.ConversationId == conversationId &&
                            cm.AccountId == currentId &&
                            !cm.HasLeft);
            var clearedAt = member?.ClearedAt;

            var query = _context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId &&
                       (clearedAt == null || m.SentAt >= clearedAt) &&
                       m.Account.Status == AccountStatusEnum.Active &&
                       !m.HiddenBy.Any(hb => hb.AccountId == currentId) &&
                       !m.IsRecalled &&
                       m.MessageType != MessageTypeEnum.System &&
                       m.Content != null && m.Content != "")
                .AsQueryable();

            // Split keyword into words, each word must match (fuzzy, accent-insensitive)
            var words = keyword.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var pattern = $"%{word}%";
                query = query.Where(m => EF.Functions.ILike(
                    AppDbContext.Unaccent(m.Content!), AppDbContext.Unaccent(pattern)));
            }

            query = query.OrderByDescending(m => m.SentAt);

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
                    IsRecalled = m.IsRecalled,
                    SystemMessageDataJson = m.SystemMessageDataJson,
                    IsPinned = false,

                    Sender = new AccountChatInfoModel
                    {
                        AccountId = m.Account.AccountId,
                        Username = m.Account.Username,
                        FullName = m.Account.FullName,
                        AvatarUrl = m.Account.AvatarUrl,
                        IsActive = m.Account.Status == AccountStatusEnum.Active
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
    }
}
