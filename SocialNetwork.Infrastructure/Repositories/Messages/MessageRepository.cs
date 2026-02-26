using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        public async Task<(IReadOnlyList<MessageBasicModel> Items, string? OlderCursor, string? NewerCursor, bool HasMoreOlder, bool HasMoreNewer)>
            GetMessagesByConversationId(Guid conversationId, Guid currentId, string? cursor, int pageSize)
        {
            if (pageSize <= 0) pageSize = 20;

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

            var offset = 0;
            if (!string.IsNullOrWhiteSpace(cursor) && int.TryParse(cursor, out var parsedOffset) && parsedOffset > 0)
            {
                offset = parsedOffset;
            }
            if (offset > totalItems)
            {
                offset = totalItems;
            }

            var messages = await query
                .Skip(offset)
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

                    Medias = new List<MessageMediaBasicModel>(),

                    ReplyTo = m.ReplyToMessageId != null ? new ReplyInfoModel
                    {
                        MessageId = m.ReplyToMessage!.MessageId,
                        Content = (m.ReplyToMessage.IsRecalled ||
                                   m.ReplyToMessage.HiddenBy.Any(hb => hb.AccountId == currentId))
                            ? null
                            : m.ReplyToMessage.Content,
                        IsRecalled = m.ReplyToMessage.IsRecalled,
                        IsHidden = m.ReplyToMessage.HiddenBy.Any(hb => hb.AccountId == currentId),
                        MessageType = m.ReplyToMessage.MessageType,
                        ReplySenderId = m.ReplyToMessage.AccountId, // For nickname lookup
                        Sender = new ReplySenderInfoModel
                        {
                            Username = m.ReplyToMessage.Account.Username,
                            DisplayName = m.ReplyToMessage.Account.Username // Will be updated in post-processing
                        }
                    } : null
                })
                .ToListAsync();

            if (messages.Count > 0)
            {
                var pageMessageIds = messages.Select(m => m.MessageId).ToList();
                var conversationMembers = _context.ConversationMembers
                    .AsNoTracking()
                    .Where(cm => cm.ConversationId == conversationId);

                // Batch-load medias for all messages in the current slice to avoid per-message subqueries
                var pageMedias = await _context.MessageMedias
                    .AsNoTracking()
                    .Where(mm => pageMessageIds.Contains(mm.MessageId))
                    .OrderBy(mm => mm.CreatedAt)
                    .Select(mm => new
                    {
                        mm.MessageId,
                        Media = new MessageMediaBasicModel
                        {
                            MessageMediaId = mm.MessageMediaId,
                            MediaUrl = mm.MediaUrl,
                            ThumbnailUrl = mm.ThumbnailUrl,
                            MediaType = mm.MediaType,
                            FileName = mm.FileName,
                            FileSize = mm.FileSize,
                            CreatedAt = mm.CreatedAt
                        }
                    })
                    .ToListAsync();
                var mediaLookup = pageMedias
                    .GroupBy(x => x.MessageId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Media).ToList());

                // Batch-load pinned messages for this page
                var pinnedMessageIds = await _context.PinnedMessages
                    .AsNoTracking()
                    .Where(pm => pm.ConversationId == conversationId && pageMessageIds.Contains(pm.MessageId))
                    .Select(pm => pm.MessageId)
                    .ToListAsync();
                var pinnedSet = pinnedMessageIds.ToHashSet();

                // Build nickname lookup for reply senders
                var memberNicknames = await conversationMembers
                    .Select(cm => new { cm.AccountId, cm.Nickname })
                    .ToDictionaryAsync(x => x.AccountId, x => x.Nickname);

                var reactedByRows = await (
                    from mr in _context.MessageReacts.AsNoTracking()
                    join cm in conversationMembers
                        on mr.AccountId equals cm.AccountId into cmGroup
                    from cm in cmGroup.DefaultIfEmpty()
                    where pageMessageIds.Contains(mr.MessageId) &&
                          mr.Account.Status == AccountStatusEnum.Active
                    select new
                    {
                        mr.MessageId,
                        mr.AccountId,
                        mr.ReactType,
                        mr.CreatedAt,
                        Username = mr.Account.Username,
                        FullName = mr.Account.FullName,
                        AvatarUrl = mr.Account.AvatarUrl,
                        Nickname = cm == null ? null : cm.Nickname
                    })
                    .ToListAsync();

                var reactLookup = reactedByRows
                    .GroupBy(x => x.MessageId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var message in messages)
                {
                    if (mediaLookup.TryGetValue(message.MessageId, out var medias))
                    {
                        message.Medias = medias;
                    }

                    message.IsPinned = pinnedSet.Contains(message.MessageId);

                    if (!reactLookup.TryGetValue(message.MessageId, out var messageReactRows))
                    {
                        continue;
                    }

                    var reactedBy = messageReactRows
                        .Select(x => new MessageReactAccountModel
                        {
                            AccountId = x.AccountId,
                            Username = x.Username,
                            FullName = x.FullName,
                            AvatarUrl = x.AvatarUrl,
                            Nickname = x.Nickname,
                            ReactType = x.ReactType,
                            CreatedAt = x.CreatedAt
                        })
                        .OrderByDescending(x => x.AccountId == currentId)
                        .ThenBy(x => x.CreatedAt)
                        .ToList();

                    message.ReactedBy = reactedBy;
                    message.Reacts = reactedBy
                        .GroupBy(x => x.ReactType)
                        .Select(g => new MessageReactSummaryModel
                        {
                            ReactType = g.Key,
                            Count = g.Count()
                        })
                        .OrderBy(x => x.ReactType)
                        .ToList();

                    message.CurrentUserReactType = reactedBy
                        .FirstOrDefault(x => x.AccountId == currentId)
                        ?.ReactType;
                }

                // Post-process: populate DisplayName for reply senders with nicknames
                foreach (var message in messages)
                {
                    if (message.ReplyTo?.Sender != null && memberNicknames.TryGetValue(message.ReplyTo.ReplySenderId, out var nickname) && !string.IsNullOrEmpty(nickname))
                    {
                        message.ReplyTo.Sender.DisplayName = nickname;
                    }
                }

                // Post-process: resolve story reply expiry (batch, only when StoryReply messages exist)
                var storyReplyMessages = messages
                    .Where(m => m.MessageType == MessageTypeEnum.StoryReply && !string.IsNullOrEmpty(m.SystemMessageDataJson))
                    .ToList();
                if (storyReplyMessages.Any())
                {
                    var storyIds = new List<Guid>();
                    var parsedSnapshots = new Dictionary<Guid, JsonElement>();
                    foreach (var msg in storyReplyMessages)
                    {
                        try
                        {
                            var doc = JsonDocument.Parse(msg.SystemMessageDataJson!);
                            parsedSnapshots[msg.MessageId] = doc.RootElement;
                            if (doc.RootElement.TryGetProperty("storyId", out var sidProp) && Guid.TryParse(sidProp.GetString(), out var sid))
                            {
                                storyIds.Add(sid);
                            }
                        }
                        catch { /* skip malformed JSON */ }
                    }

                    var followedIdsQuery = _context.Follows
                        .AsNoTracking()
                        .Where(f => f.FollowerId == currentId)
                        .Select(f => f.FollowedId);

                    var activeStoryIdsList = storyIds.Any()
                        ? await _context.Stories.AsNoTracking()
                            .Where(s => storyIds.Contains(s.StoryId) 
                                        && s.ExpiresAt > DateTime.UtcNow 
                                        && !s.IsDeleted
                                        && s.Account.Status == AccountStatusEnum.Active
                                        && (
                                            s.AccountId == currentId ||
                                            s.Privacy == StoryPrivacyEnum.Public ||
                                            (s.Privacy == StoryPrivacyEnum.FollowOnly && followedIdsQuery.Contains(s.AccountId))
                                        ))
                            .Select(s => s.StoryId)
                            .ToListAsync()
                        : new List<Guid>();
                    var activeStoryIds = activeStoryIdsList.ToHashSet();

                    foreach (var msg in storyReplyMessages)
                    {
                        if (!parsedSnapshots.TryGetValue(msg.MessageId, out var root)) continue;
                        var storyId = Guid.Empty;
                        if (root.TryGetProperty("storyId", out var sp)) Guid.TryParse(sp.GetString(), out storyId);
                        var isExpired = storyId == Guid.Empty || !activeStoryIds.Contains(storyId);

                        msg.StoryReplyInfo = new StoryReplyInfoModel
                        {
                            StoryId = storyId,
                            IsStoryExpired = isExpired,
                            MediaUrl = isExpired ? null : (root.TryGetProperty("mediaUrl", out var mu) ? mu.GetString() : null),
                            ContentType = root.TryGetProperty("contentType", out var ct) ? ct.GetInt32() : 0,
                            TextContent = isExpired ? null : (root.TryGetProperty("textContent", out var tc) ? tc.GetString() : null),
                            BackgroundColorKey = isExpired ? null : (root.TryGetProperty("backgroundColorKey", out var bg) ? bg.GetString() : null),
                            TextColorKey = isExpired ? null : (root.TryGetProperty("textColorKey", out var tk) ? tk.GetString() : null),
                            FontTextKey = isExpired ? null : (root.TryGetProperty("fontTextKey", out var ft) ? ft.GetString() : null),
                            FontSizeKey = isExpired ? null : (root.TryGetProperty("fontSizeKey", out var fz) ? fz.GetString() : null),
                        };
                    }
                }
            }

            var hasMoreOlder = offset + messages.Count < totalItems;
            var hasMoreNewer = offset > 0;
            var olderCursor = hasMoreOlder ? (offset + messages.Count).ToString() : null;
            var newerCursor = hasMoreNewer ? Math.Max(0, offset - pageSize).ToString() : null;

            return (messages, olderCursor, newerCursor, hasMoreOlder, hasMoreNewer);
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
            return await _context.Messages
                .Include(m => m.Account)
                .FirstOrDefaultAsync(m => m.MessageId == messageId);
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

                    Medias = new List<MessageMediaBasicModel>(),

                    ReplyTo = m.ReplyToMessageId != null ? new ReplyInfoModel
                    {
                        MessageId = m.ReplyToMessage!.MessageId,
                        Content = (m.ReplyToMessage.IsRecalled ||
                                   m.ReplyToMessage.HiddenBy.Any(hb => hb.AccountId == currentId))
                            ? null
                            : m.ReplyToMessage.Content,
                        IsRecalled = m.ReplyToMessage.IsRecalled,
                        IsHidden = m.ReplyToMessage.HiddenBy.Any(hb => hb.AccountId == currentId),
                        MessageType = m.ReplyToMessage.MessageType,
                        ReplySenderId = m.ReplyToMessage.AccountId, // For nickname lookup
                        Sender = new ReplySenderInfoModel
                        {
                            Username = m.ReplyToMessage.Account.Username,
                            DisplayName = m.ReplyToMessage.Account.Username // Will be updated in post-processing
                        }
                    } : null
                })
                .ToListAsync();

            // Post-process: populate medias + DisplayName for reply senders with nicknames
            if (messages.Count > 0)
            {
                var messageIds = messages.Select(m => m.MessageId).ToList();
                var mediaRows = await _context.MessageMedias
                    .AsNoTracking()
                    .Where(mm => messageIds.Contains(mm.MessageId))
                    .OrderBy(mm => mm.CreatedAt)
                    .Select(mm => new
                    {
                        mm.MessageId,
                        Media = new MessageMediaBasicModel
                        {
                            MessageMediaId = mm.MessageMediaId,
                            MediaUrl = mm.MediaUrl,
                            ThumbnailUrl = mm.ThumbnailUrl,
                            MediaType = mm.MediaType,
                            FileName = mm.FileName,
                            FileSize = mm.FileSize,
                            CreatedAt = mm.CreatedAt
                        }
                    })
                    .ToListAsync();
                var mediaLookup = mediaRows
                    .GroupBy(x => x.MessageId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Media).ToList());

                var memberNicknames = await _context.ConversationMembers
                    .AsNoTracking()
                    .Where(cm => cm.ConversationId == conversationId)
                    .Select(cm => new { cm.AccountId, cm.Nickname })
                    .ToDictionaryAsync(x => x.AccountId, x => x.Nickname);

                foreach (var message in messages)
                {
                    if (mediaLookup.TryGetValue(message.MessageId, out var medias))
                    {
                        message.Medias = medias;
                    }

                    if (message.ReplyTo?.Sender != null && memberNicknames.TryGetValue(message.ReplyTo.ReplySenderId, out var nickname) && !string.IsNullOrEmpty(nickname))
                    {
                        message.ReplyTo.Sender.DisplayName = nickname;
                    }
                }
            }

            return (messages, totalItems);
        }
    }
}
