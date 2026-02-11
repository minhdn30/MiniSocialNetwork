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
            return await _context.Conversations.AnyAsync(c =>
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
                .Where(cm => cm.AccountId == currentId && !cm.Conversation.IsDeleted && cm.Conversation.Messages.Any(m => !m.HiddenBy.Any(hb => hb.AccountId == currentId)));

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
                cm.LastSeenAt,
                // Correlation subquery for sorting by last message
                LastMessageSentAt = (DateTime?)_context.Messages
                    .Where(m => m.ConversationId == cm.ConversationId && !m.HiddenBy.Any(hb => hb.AccountId == currentId))
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

            // Fetch OtherMember info separately
            var privateConvIds = pagedData.Where(x => !x.IsGroup).Select(x => x.ConversationId).ToList();
            var otherMembers = await _context.ConversationMembers
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

            // Fetch last messages one by one or using a much safer bulk approach
            // For 20 items, this is very safe and avoids complex GroupBy translation issues
            var lastMessages = new List<(Guid ConvId, MessageBasicModel Message)>();
            foreach (var convId in conversationIds)
            {
                var m = await _context.Messages
                    .Where(msg => msg.ConversationId == convId && !msg.HiddenBy.Any(hb => hb.AccountId == currentId))
                    .OrderByDescending(msg => msg.SentAt)
                    .Select(msg => new MessageBasicModel
                    {
                        MessageId = msg.MessageId,
                        Content = msg.Content,
                        MessageType = msg.MessageType,
                        SentAt = msg.SentAt,
                        IsEdited = msg.IsEdited,
                        IsRecalled = msg.IsRecalled,
                        Sender = new AccountChatInfoModel
                        {
                            AccountId = msg.Account.AccountId,
                            FullName = msg.Account.FullName,
                            AvatarUrl = msg.Account.AvatarUrl,
                            Username = msg.Account.Username,
                            IsActive = msg.Account.Status == AccountStatusEnum.Active
                        },
                        Medias = msg.Medias.Select(med => new MessageMediaBasicModel
                        {
                            MessageMediaId = med.MessageMediaId,
                            MediaUrl = med.MediaUrl,
                            MediaType = med.MediaType
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (m != null)
                {
                    lastMessages.Add((convId, m));
                }
            }

            // Bulk fetch unread counts - Simplified
            var unreadCounts = await _context.ConversationMembers
                .Where(cm => cm.AccountId == currentId && conversationIds.Contains(cm.ConversationId))
                .Select(cm => new {
                    cm.ConversationId,
                    Count = _context.Messages.Count(m => m.ConversationId == cm.ConversationId && m.AccountId != cm.AccountId && 
                    !m.HiddenBy.Any(hb => hb.AccountId == currentId) && (!cm.LastSeenAt.HasValue || m.SentAt > cm.LastSeenAt.Value))
                })
                .ToListAsync();

            // Identify conversations where last message is from current user (for seen-by display)
            var myLastMsgConvIds = lastMessages
                .Where(lm => lm.Message.Sender.AccountId == currentId)
                .Select(lm => lm.ConvId)
                .ToList();

            // Bulk fetch members who have seen the last message (for "seen by" avatars)
            var seenByMembers = new List<(Guid ConvId, SeenByMemberInfo Info)>();
            var seenCountMap = new Dictionary<Guid, int>();
            if (myLastMsgConvIds.Count > 0)
            {
                var lastMsgSentAts = lastMessages
                    .Where(lm => myLastMsgConvIds.Contains(lm.ConvId))
                    .ToDictionary(lm => lm.ConvId, lm => lm.Message.SentAt);

                foreach (var convId in myLastMsgConvIds)
                {
                    if (!lastMsgSentAts.TryGetValue(convId, out var sentAt)) continue;

                    var baseSeenQuery = _context.ConversationMembers
                        .Where(cm => cm.ConversationId == convId
                            && cm.AccountId != currentId
                            && !cm.HasLeft
                            && cm.LastSeenAt.HasValue
                            && cm.LastSeenAt.Value >= sentAt);

                    var seenCount = await baseSeenQuery.CountAsync();
                    seenCountMap[convId] = seenCount;

                    var seenMembers = await baseSeenQuery
                        .OrderByDescending(cm => cm.LastSeenAt)
                        .Take(3)
                        .Select(cm => new SeenByMemberInfo
                        {
                            AccountId = cm.AccountId,
                            AvatarUrl = cm.Account.AvatarUrl,
                            DisplayName = cm.Nickname ?? cm.Account.Username
                        })
                        .ToListAsync();

                    foreach (var m in seenMembers)
                    {
                        seenByMembers.Add((convId, m));
                    }
                }
            }

            var result = pagedData.Select(item =>
            {
                var lastMsgEntry = lastMessages.FirstOrDefault(lm => lm.ConvId == item.ConversationId);
                var unreadInfo = unreadCounts.FirstOrDefault(uc => uc.ConversationId == item.ConversationId);
                var unreadCount = unreadInfo?.Count ?? 0;
                var otherMember = otherMembers.FirstOrDefault(om => om.ConversationId == item.ConversationId)?.Info;

                string? displayName = item.IsGroup ? item.ConversationName : (otherMember?.Nickname ?? otherMember?.Username);
                string? displayAvatar = item.IsGroup ? item.ConversationAvatar : otherMember?.AvatarUrl;

                var seenBy = seenByMembers
                    .Where(s => s.ConvId == item.ConversationId)
                    .Select(s => s.Info)
                    .ToList();
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
                    LastSeenAt = item.LastSeenAt,
                    UnreadCount = unreadCount,
                    IsRead = unreadCount == 0,
                    LastMessage = lastMsgEntry.Message,
                    LastMessageSentAt = item.LastMessageSentAt,
                    LastMessageSeenBy = seenBy.Count > 0 ? seenBy : null,
                    LastMessageSeenCount = seenCount
                };
            }).ToList();

            return (result, totalCount);
        }

        public async Task<ConversationListModel?> GetConversationMetaDataAsync(Guid conversationId, Guid currentId)
        {
            var cm = await _context.ConversationMembers
                .Include(x => x.Conversation)
                .FirstOrDefaultAsync(x => x.ConversationId == conversationId && x.AccountId == currentId && !x.Conversation.IsDeleted);

            if (cm == null) return null;

            OtherMemberBasicInfo? otherMember = null;
            if (!cm.Conversation.IsGroup)
            {
                otherMember = await _context.ConversationMembers
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
                .CountAsync(m => m.ConversationId == conversationId && m.AccountId != currentId && !m.HiddenBy.Any(hb => hb.AccountId == currentId) && (!cm.LastSeenAt.HasValue || m.SentAt > cm.LastSeenAt.Value));

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
                LastSeenAt = cm.LastSeenAt,
                UnreadCount = unreadCount,
                IsRead = unreadCount == 0
            };
        }

        // lightweight query - only returns conversation id without includes
        public async Task<Guid?> GetPrivateConversationIdAsync(Guid accountId1, Guid accountId2)
        {
            return await _context.Conversations
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
                .Where(c => c.ConversationId == conversationId && !c.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetUnreadConversationCountAsync(Guid currentId)
        {
            return await _context.ConversationMembers
                .Where(cm => cm.AccountId == currentId
                    && !cm.HasLeft
                    && !cm.IsMuted
                    && !cm.Conversation.IsDeleted
                    && cm.Conversation.Messages.Any())
                .CountAsync(cm =>
                    _context.Messages.Any(m =>
                        m.ConversationId == cm.ConversationId
                        && m.AccountId != currentId
                        && !m.HiddenBy.Any(hb => hb.AccountId == currentId)
                        && (!cm.LastSeenAt.HasValue || m.SentAt > cm.LastSeenAt.Value)
                    )
                );
        }
    }
}
