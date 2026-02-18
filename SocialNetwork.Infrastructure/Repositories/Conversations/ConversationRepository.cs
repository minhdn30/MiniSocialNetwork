using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
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
                .AsNoTracking()
                .Where(c => !c.IsDeleted && !c.IsGroup)
                .Where(c => c.Members.Count == 2 
                            && c.Members.Any(m => m.AccountId == accountId1 && m.Account.Status == AccountStatusEnum.Active) 
                            && c.Members.Any(m => m.AccountId == accountId2 && m.Account.Status == AccountStatusEnum.Active))
                .Include(c => c.Members)
                    .ThenInclude(m => m.Account)
                .FirstOrDefaultAsync();
        }

        public Task AddConversationAsync(Conversation conversation)
        {
            _context.Conversations.Add(conversation);
            return Task.CompletedTask;
        } 

        public async Task<bool> IsPrivateConversationExistBetweenTwoAccounts(Guid accountId1, Guid accountId2)
        {
            return await _context.Conversations
                .AsNoTracking()
                .AnyAsync(c =>
                !c.IsDeleted &&
                !c.IsGroup &&
                c.Members.Count == 2 &&
                c.Members.Any(m => m.AccountId == accountId1 && m.Account.Status == AccountStatusEnum.Active) &&
                c.Members.Any(m => m.AccountId == accountId2 && m.Account.Status == AccountStatusEnum.Active)
            );
        }

        public Task<Conversation> CreatePrivateConversationAsync(Guid currentId, Guid otherId)
        {
            var conversation = new Conversation
            {
                ConversationId = Guid.NewGuid(),
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
            return Task.FromResult(conversation);
        }

        public async Task<(List<ConversationListModel> Items, int TotalCount)> GetConversationsPagedAsync(Guid currentId, bool? isPrivate, string? search, int page, int pageSize)
        {
            var baseQuery = _context.ConversationMembers
                .AsNoTracking()
                .Where(cm => cm.AccountId == currentId && !cm.Conversation.IsDeleted 
                && cm.Conversation.Messages.Any(m => (!cm.ClearedAt.HasValue || m.SentAt >= cm.ClearedAt.Value) 
                && !m.HiddenBy.Any(hb => hb.AccountId == currentId)));

            if (isPrivate.HasValue)
            {
                if (isPrivate.Value)
                {
                    baseQuery = baseQuery.Where(cm => !cm.Conversation.IsGroup);
                }
                else
                {
                    baseQuery = baseQuery.Where(cm => cm.Conversation.IsGroup);
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchPattern = $"%{search.Trim()}%";
                baseQuery = baseQuery.Where(cm =>
                    (cm.Conversation.IsGroup && EF.Functions.ILike(cm.Conversation.ConversationName!, searchPattern)) ||
                    (!cm.Conversation.IsGroup && cm.Conversation.Members.Any(m => m.AccountId != currentId
                        && (EF.Functions.ILike(m.Account.FullName, searchPattern)
                            || EF.Functions.ILike(m.Account.Username, searchPattern))))
                );
            }

            var totalCount = await baseQuery.CountAsync();

            // Simplest possible projection that includes the sort key
            var query = baseQuery.Select(cm => new
            {
                cm.ConversationId,
                cm.Conversation.IsGroup,
                cm.Conversation.ConversationName,
                cm.Conversation.ConversationAvatar,
                cm.Conversation.Theme,
                cm.LastSeenAt,
                cm.IsMuted,
                // Correlation subquery for sorting by last message
                LastMessageSentAt = (DateTime?)_context.Messages
                    .Where(m => m.ConversationId == cm.ConversationId && (!cm.ClearedAt.HasValue || m.SentAt >= cm.ClearedAt.Value) 
                    && !m.HiddenBy.Any(hb => hb.AccountId == currentId))
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.SentAt)
                    .FirstOrDefault()
            });

            var pagedData = await query
                .OrderByDescending(x => x.LastMessageSentAt)
                .ThenByDescending(x => x.ConversationId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var conversationIds = pagedData.Select(c => c.ConversationId).ToList();
            if (conversationIds.Count == 0)
            {
                return (new List<ConversationListModel>(), totalCount);
            }

            // Fetch OtherMember info separately
            var privateConvIds = pagedData.Where(x => !x.IsGroup).Select(x => x.ConversationId).ToList();
            var otherMembers = await _context.ConversationMembers
                .AsNoTracking()
                .Where(cm => privateConvIds.Contains(cm.ConversationId) && cm.AccountId != currentId)
                .Select(cm => new {
                    cm.ConversationId,
                    Info = new OtherMemberBasicInfo
                    {
                        AccountId = cm.AccountId,
                        Username = cm.Account.Username,
                        FullName = cm.Account.FullName,
                        Nickname = cm.Nickname,
                        AvatarUrl = cm.Account.AvatarUrl,
                        IsActive = cm.Account.Status == AccountStatusEnum.Active
                    }
                })
                .ToListAsync();
            var otherMemberMap = otherMembers
                .GroupBy(x => x.ConversationId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Info).FirstOrDefault());

            // Batch fetch latest message reference per conversation (top 1 by SentAt desc, MessageId desc)
            var lastMessageRefs = await _context.ConversationMembers
                .AsNoTracking()
                .Where(cm => cm.AccountId == currentId && conversationIds.Contains(cm.ConversationId))
                .Select(cm => new
                {
                    cm.ConversationId,
                    MessageId = _context.Messages
                        .Where(m => m.ConversationId == cm.ConversationId
                            && (!cm.ClearedAt.HasValue || m.SentAt >= cm.ClearedAt.Value)
                            && !m.HiddenBy.Any(hb => hb.AccountId == currentId))
                        .OrderByDescending(m => m.SentAt)
                        .ThenByDescending(m => m.MessageId)
                        .Select(m => (Guid?)m.MessageId)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var lastMessageRefMap = lastMessageRefs
                .Where(x => x.MessageId.HasValue)
                .ToDictionary(x => x.ConversationId, x => x.MessageId!.Value);

            var lastMessageIds = lastMessageRefMap.Values.Distinct().ToList();
            var lastMessageRows = new List<(Guid ConversationId, MessageBasicModel Message)>();
            if (lastMessageIds.Count > 0)
            {
                lastMessageRows = await _context.Messages
                    .AsNoTracking()
                    .Where(msg => lastMessageIds.Contains(msg.MessageId))
                    .Select(msg => new ValueTuple<Guid, MessageBasicModel>(
                        msg.ConversationId,
                        new MessageBasicModel
                        {
                            MessageId = msg.MessageId,
                            Content = msg.Content,
                            MessageType = msg.MessageType,
                            SentAt = msg.SentAt,
                            IsEdited = msg.IsEdited,
                            IsRecalled = msg.IsRecalled,
                            SystemMessageDataJson = msg.SystemMessageDataJson,
                            Sender = new AccountChatInfoModel
                            {
                                AccountId = msg.Account.AccountId,
                                FullName = msg.Account.FullName,
                                AvatarUrl = msg.Account.AvatarUrl,
                                Username = msg.Account.Username,
                                IsActive = msg.Account.Status == AccountStatusEnum.Active
                            },
                            Medias = new List<MessageMediaBasicModel>()
                        }))
                    .ToListAsync();

                // Batch-load medias for last messages to avoid per-message correlated subqueries
                var mediaRows = await _context.MessageMedias
                    .AsNoTracking()
                    .Where(med => lastMessageIds.Contains(med.MessageId))
                    .Select(med => new
                    {
                        med.MessageId,
                        Media = new MessageMediaBasicModel
                        {
                            MessageMediaId = med.MessageMediaId,
                            MediaUrl = med.MediaUrl,
                            MediaType = med.MediaType
                        }
                    })
                    .ToListAsync();
                var mediaLookup = mediaRows
                    .GroupBy(x => x.MessageId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Media).ToList());

                for (var i = 0; i < lastMessageRows.Count; i++)
                {
                    var row = lastMessageRows[i];
                    if (mediaLookup.TryGetValue(row.Message.MessageId, out var medias))
                    {
                        row.Message.Medias = medias;
                        lastMessageRows[i] = row;
                    }
                }
            }

            var lastMessageMap = lastMessageRows
                .GroupBy(x => x.ConversationId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Message).First());

            // Bulk fetch unread counts - Simplified
            var unreadCounts = await _context.ConversationMembers
                .AsNoTracking()
                .Where(cm => cm.AccountId == currentId && conversationIds.Contains(cm.ConversationId))
                .Select(cm => new {
                    cm.ConversationId,
                    Count = _context.Messages.Count(m => m.ConversationId == cm.ConversationId && m.AccountId != cm.AccountId && 
                    !m.HiddenBy.Any(hb => hb.AccountId == currentId) && (!cm.ClearedAt.HasValue || m.SentAt >= cm.ClearedAt.Value) 
                    && (!cm.LastSeenAt.HasValue || m.SentAt > cm.LastSeenAt.Value))
                })
                .ToListAsync();
            var unreadMap = unreadCounts.ToDictionary(x => x.ConversationId, x => x.Count);

            // Identify conversations where last message is from current user (for seen-by display)
            var myLastMsgConvIds = lastMessageMap
                .Where(lm => lm.Value.Sender.AccountId == currentId)
                .Select(lm => lm.Key)
                .ToList();

            var seenCountMap = new Dictionary<Guid, int>();
            var seenByMap = new Dictionary<Guid, List<SeenByMemberInfo>>();
            if (myLastMsgConvIds.Count > 0)
            {
                var lastMsgSentAtMap = myLastMsgConvIds
                    .ToDictionary(convId => convId, convId => lastMessageMap[convId].SentAt);

                var seenCandidates = await _context.ConversationMembers
                    .AsNoTracking()
                    .Where(cm => myLastMsgConvIds.Contains(cm.ConversationId)
                        && cm.AccountId != currentId
                        && !cm.HasLeft
                        && cm.LastSeenAt.HasValue)
                    .Select(cm => new
                    {
                        cm.ConversationId,
                        LastSeenAt = cm.LastSeenAt!.Value,
                        Info = new SeenByMemberInfo
                        {
                            AccountId = cm.AccountId,
                            AvatarUrl = cm.Account.AvatarUrl,
                            DisplayName = cm.Nickname ?? cm.Account.Username
                        }
                    })
                    .ToListAsync();

                var filteredSeen = seenCandidates
                    .Where(x => lastMsgSentAtMap.TryGetValue(x.ConversationId, out var sentAt) && x.LastSeenAt >= sentAt)
                    .ToList();

                foreach (var group in filteredSeen.GroupBy(x => x.ConversationId))
                {
                    var ordered = group.OrderByDescending(x => x.LastSeenAt).ToList();
                    seenCountMap[group.Key] = ordered.Count;
                    seenByMap[group.Key] = ordered.Take(3).Select(x => x.Info).ToList();
                }
            }

            var result = pagedData.Select(item =>
            {
                lastMessageMap.TryGetValue(item.ConversationId, out var lastMessage);
                unreadMap.TryGetValue(item.ConversationId, out var unreadCount);
                otherMemberMap.TryGetValue(item.ConversationId, out var otherMember);

                string? displayName = item.IsGroup ? item.ConversationName : (otherMember?.Nickname ?? otherMember?.Username);
                string? displayAvatar = item.IsGroup ? item.ConversationAvatar : otherMember?.AvatarUrl;

                seenByMap.TryGetValue(item.ConversationId, out var seenBy);
                var seenCount = seenCountMap.TryGetValue(item.ConversationId, out var count) ? count : 0;

                return new ConversationListModel
                {
                    ConversationId = item.ConversationId,
                    IsGroup = item.IsGroup,
                    DisplayName = displayName,
                    DisplayAvatar = displayAvatar,
                    OtherMember = otherMember,
                    ConversationName = item.ConversationName,
                    ConversationAvatar = item.ConversationAvatar,
                    Theme = item.Theme,
                    LastSeenAt = item.LastSeenAt,
                    IsMuted = item.IsMuted,
                    UnreadCount = unreadCount,
                    IsRead = unreadCount == 0,
                    LastMessage = lastMessage,
                    LastMessageSentAt = item.LastMessageSentAt,
                    LastMessageSeenBy = seenBy != null && seenBy.Count > 0 ? seenBy : null,
                    LastMessageSeenCount = seenCount
                    
                };
            }).ToList();

            return (result, totalCount);
        }

        public async Task<ConversationListModel?> GetConversationMetaDataAsync(Guid conversationId, Guid currentId)
        {
            var cm = await _context.ConversationMembers
                .AsNoTracking()
                .Include(x => x.Conversation)
                .FirstOrDefaultAsync(x => x.ConversationId == conversationId && x.AccountId == currentId && !x.Conversation.IsDeleted);

            if (cm == null) return null;

            OtherMemberBasicInfo? otherMember = null;
            if (!cm.Conversation.IsGroup)
            {
                otherMember = await _context.ConversationMembers
                    .AsNoTracking()
                    .Where(x => x.ConversationId == conversationId && x.AccountId != currentId)
                    .Select(x => new OtherMemberBasicInfo
                    {
                        AccountId = x.AccountId,
                        Username = x.Account.Username,
                        FullName = x.Account.FullName,
                        Nickname = x.Nickname,
                        AvatarUrl = x.Account.AvatarUrl,
                        IsActive = x.Account.Status == AccountStatusEnum.Active
                    })
                    .FirstOrDefaultAsync();
            }

            var unreadCount = await _context.Messages
                .AsNoTracking()
                .CountAsync(m => m.ConversationId == conversationId && m.AccountId != currentId && !m.HiddenBy.Any(hb => hb.AccountId == currentId) 
                && (!cm.ClearedAt.HasValue || m.SentAt >= cm.ClearedAt.Value) && (!cm.LastSeenAt.HasValue || m.SentAt > cm.LastSeenAt.Value));

            string? displayName = cm.Conversation.IsGroup ? cm.Conversation.ConversationName : (otherMember?.Nickname ?? otherMember?.Username);
            string? displayAvatar = cm.Conversation.IsGroup ? cm.Conversation.ConversationAvatar : otherMember?.AvatarUrl;

            return new ConversationListModel
            {
                ConversationId = cm.ConversationId,
                IsGroup = cm.Conversation.IsGroup,
                DisplayName = displayName,
                DisplayAvatar = displayAvatar,
                OtherMember = otherMember,
                ConversationName = cm.Conversation.ConversationName,
                ConversationAvatar = cm.Conversation.ConversationAvatar,
                Theme = cm.Conversation.Theme,
                LastSeenAt = cm.LastSeenAt,
                UnreadCount = unreadCount,
                IsRead = unreadCount == 0,
                IsMuted = cm.IsMuted
            };
        }

        // lightweight query - only returns conversation id without includes
        public async Task<Guid?> GetPrivateConversationIdAsync(Guid accountId1, Guid accountId2)
        {
            return await _context.Conversations
                .AsNoTracking()
                .Where(c => !c.IsDeleted && !c.IsGroup)
                .Where(c => c.Members.Count == 2
                            && c.Members.Any(m => m.AccountId == accountId1)
                            && c.Members.Any(m => m.AccountId == accountId2))
                .Select(c => (Guid?)c.ConversationId)
                .FirstOrDefaultAsync();
        }

        public async Task<Conversation?> GetConversationByIdAsync(Guid conversationId)
        {
            return await _context.Conversations
                .AsNoTracking()
                .Where(c => c.ConversationId == conversationId && !c.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public Task UpdateConversationAsync(Conversation conversation)
        {
            _context.Conversations.Update(conversation);
            return Task.CompletedTask;
        }

        public async Task<int> GetUnreadConversationCountAsync(Guid currentId)
        {
            return await _context.ConversationMembers
                .AsNoTracking()
                .Where(cm => cm.AccountId == currentId
                    && !cm.HasLeft
                    && !cm.IsMuted
                    && !cm.Conversation.IsDeleted)
                .CountAsync(cm =>
                    _context.Messages.Any(m =>
                        m.ConversationId == cm.ConversationId
                        && m.AccountId != currentId
                        && !m.HiddenBy.Any(hb => hb.AccountId == currentId)
                        && (!cm.ClearedAt.HasValue || m.SentAt >= cm.ClearedAt.Value)
                        && (!cm.LastSeenAt.HasValue || m.SentAt > cm.LastSeenAt.Value)
                    )
                );
        }
    }
}
