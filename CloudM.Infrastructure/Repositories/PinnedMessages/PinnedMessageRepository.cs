using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

        public async Task<(IReadOnlyList<PinnedMessageModel> Items, int TotalItems)> GetPinnedMessagesByConversationIdAsync(
            Guid conversationId,
            DateTime? clearedAt,
            Guid currentAccountId,
            int page,
            int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = _context.PinnedMessages
                .AsNoTracking()
                .Where(pm => pm.ConversationId == conversationId)
                // filter clearedAt for current user
                .Where(pm => clearedAt == null || pm.Message.SentAt >= clearedAt)
                // filter hiddenBy for current user
                .Where(pm => !pm.Message.HiddenBy.Any(hb => hb.AccountId == currentAccountId))
                // recalled message is treated as removed from pinned list
                .Where(pm => !pm.Message.IsRecalled)
                .Where(pm =>
                    pm.Message.Account.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(pm.Message.Account.RoleId) &&
                    pm.PinnedByAccount.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(pm.PinnedByAccount.RoleId))
                .OrderByDescending(pm => pm.PinnedAt);

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(pm => new PinnedMessageModel
                {
                    MessageId = pm.MessageId,
                    ConversationId = pm.ConversationId,
                    Content = pm.Message.IsRecalled ? null : pm.Message.Content,
                    MessageType = pm.Message.MessageType,
                    SentAt = pm.Message.SentAt,
                    IsRecalled = pm.Message.IsRecalled,
                    HasReply = pm.Message.ReplyToMessageId != null,
                    SystemMessageDataJson = pm.Message.IsRecalled ? null : pm.Message.SystemMessageDataJson,
                    PinnedAt = pm.PinnedAt,
                    Sender = new AccountChatInfoModel
                    {
                        AccountId = pm.Message.Account.AccountId,
                        Username = pm.Message.Account.Username,
                        FullName = pm.Message.Account.FullName,
                        AvatarUrl = pm.Message.Account.AvatarUrl,
                        IsActive = pm.Message.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(pm.Message.Account.RoleId)
                    },
                    PinnedByAccount = new AccountChatInfoModel
                    {
                        AccountId = pm.PinnedByAccount.AccountId,
                        Username = pm.PinnedByAccount.Username,
                        FullName = pm.PinnedByAccount.FullName,
                        AvatarUrl = pm.PinnedByAccount.AvatarUrl,
                        IsActive = pm.PinnedByAccount.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(pm.PinnedByAccount.RoleId)
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
                        }).ToList(),
                    ReplyTo = pm.Message.ReplyToMessageId != null ? new ReplyInfoModel
                    {
                        MessageId = pm.Message.ReplyToMessage!.MessageId,
                        Content = (pm.Message.ReplyToMessage.IsRecalled ||
                                   pm.Message.ReplyToMessage.HiddenBy.Any(hb => hb.AccountId == currentAccountId))
                            ? null
                            : pm.Message.ReplyToMessage.Content,
                        IsRecalled = pm.Message.ReplyToMessage.IsRecalled,
                        IsHidden = pm.Message.ReplyToMessage.HiddenBy.Any(hb => hb.AccountId == currentAccountId),
                        MessageType = pm.Message.ReplyToMessage.MessageType,
                        ReplySenderId = pm.Message.ReplyToMessage.AccountId,
                        Sender = new ReplySenderInfoModel
                        {
                            Username = pm.Message.ReplyToMessage.Account.Username,
                            DisplayName = pm.Message.ReplyToMessage.Account.Username
                        }
                    } : null
                })
                .ToListAsync();

            if (items.Count == 0)
            {
                return (items, totalItems);
            }

            var memberNicknames = await _context.ConversationMembers
                .AsNoTracking()
                .Where(cm => cm.ConversationId == conversationId)
                .Select(cm => new { cm.AccountId, cm.Nickname })
                .ToDictionaryAsync(x => x.AccountId, x => x.Nickname);

            foreach (var message in items)
            {
                if (message.ReplyTo?.Sender != null &&
                    memberNicknames.TryGetValue(message.ReplyTo.ReplySenderId, out var nickname) &&
                    !string.IsNullOrEmpty(nickname))
                {
                    message.ReplyTo.Sender.DisplayName = nickname;
                }
            }

            // resolve story reply payload and story visibility for pinned messages
            var storyReplyMessages = items
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
                        using var doc = JsonDocument.Parse(msg.SystemMessageDataJson!);
                        var root = doc.RootElement.Clone();
                        parsedSnapshots[msg.MessageId] = root;
                        if (root.TryGetProperty("storyId", out var sidProp) &&
                            Guid.TryParse(sidProp.GetString(), out var sid))
                        {
                            storyIds.Add(sid);
                        }
                    }
                    catch
                    {
                        // ignore malformed snapshots and fallback to unavailable state
                    }
                }

                var followedIdsQuery = _context.Follows
                    .AsNoTracking()
                    .Where(f => f.FollowerId == currentAccountId)
                    .Select(f => f.FollowedId);

                var activeStoryIdsList = storyIds.Any()
                    ? await _context.Stories.AsNoTracking()
                        .Where(s => storyIds.Contains(s.StoryId)
                                    && s.ExpiresAt > DateTime.UtcNow
                                    && !s.IsDeleted
                                    && s.Account.Status == AccountStatusEnum.Active
                                    && SocialRoleRules.SocialEligibleRoleIds.Contains(s.Account.RoleId)
                                    && (
                                        s.AccountId == currentAccountId ||
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
                    if (root.TryGetProperty("storyId", out var sidProp))
                    {
                        Guid.TryParse(sidProp.GetString(), out storyId);
                    }

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

            // resolve post share payload and post visibility for pinned messages
            var postShareMessages = items
                .Where(m => !m.IsRecalled && m.MessageType == MessageTypeEnum.PostShare && !string.IsNullOrEmpty(m.SystemMessageDataJson))
                .ToList();
            if (postShareMessages.Any())
            {
                var postIds = new List<Guid>();
                var parsedSnapshots = new Dictionary<Guid, JsonElement>();
                foreach (var msg in postShareMessages)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(msg.SystemMessageDataJson!);
                        var root = doc.RootElement.Clone();
                        parsedSnapshots[msg.MessageId] = root;
                        if (root.TryGetProperty("postId", out var postIdProp) &&
                            Guid.TryParse(postIdProp.GetString(), out var postId))
                        {
                            postIds.Add(postId);
                        }
                    }
                    catch
                    {
                        // ignore malformed snapshots and fallback to unavailable state
                    }
                }

                var followedIdsQuery = _context.Follows
                    .AsNoTracking()
                    .Where(f => f.FollowerId == currentAccountId)
                    .Select(f => f.FollowedId);

                var visiblePostIdsList = postIds.Any()
                    ? await _context.Posts.AsNoTracking()
                        .Where(p => postIds.Contains(p.PostId)
                                    && !p.IsDeleted
                                    && p.Account.Status == AccountStatusEnum.Active
                                    && SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId)
                                    && (
                                        p.AccountId == currentAccountId ||
                                        p.Privacy == PostPrivacyEnum.Public ||
                                        (p.Privacy == PostPrivacyEnum.FollowOnly && followedIdsQuery.Contains(p.AccountId))
                                    ))
                        .Select(p => p.PostId)
                        .ToListAsync()
                    : new List<Guid>();
                var visiblePostIds = visiblePostIdsList.ToHashSet();

                foreach (var msg in postShareMessages)
                {
                    if (!parsedSnapshots.TryGetValue(msg.MessageId, out var root))
                    {
                        msg.PostShareInfo = new PostShareInfoModel
                        {
                            IsPostUnavailable = true
                        };
                        continue;
                    }

                    var postId = Guid.Empty;
                    if (root.TryGetProperty("postId", out var postIdProp))
                    {
                        Guid.TryParse(postIdProp.GetString(), out postId);
                    }

                    var postCode = root.TryGetProperty("postCode", out var postCodeProp)
                        ? postCodeProp.GetString() ?? string.Empty
                        : string.Empty;

                    var ownerId = Guid.Empty;
                    if (root.TryGetProperty("ownerId", out var ownerIdProp))
                    {
                        Guid.TryParse(ownerIdProp.GetString(), out ownerId);
                    }

                    var ownerUsername = root.TryGetProperty("ownerUsername", out var ownerUsernameProp)
                        ? ownerUsernameProp.GetString()
                        : null;
                    var ownerDisplayName = root.TryGetProperty("ownerDisplayName", out var ownerDisplayNameProp)
                        ? ownerDisplayNameProp.GetString()
                        : null;
                    var thumbnailUrl = root.TryGetProperty("thumbnailUrl", out var thumbnailUrlProp)
                        ? thumbnailUrlProp.GetString()
                        : null;
                    int? thumbnailMediaType = null;
                    if (root.TryGetProperty("thumbnailMediaType", out var thumbnailMediaTypeProp))
                    {
                        if (thumbnailMediaTypeProp.ValueKind == JsonValueKind.Number &&
                            thumbnailMediaTypeProp.TryGetInt32(out var numericMediaType))
                        {
                            thumbnailMediaType = numericMediaType;
                        }
                        else if (thumbnailMediaTypeProp.ValueKind == JsonValueKind.String &&
                                 int.TryParse(thumbnailMediaTypeProp.GetString(), out var parsedMediaType))
                        {
                            thumbnailMediaType = parsedMediaType;
                        }
                    }
                    var contentSnippet = root.TryGetProperty("contentSnippet", out var contentSnippetProp)
                        ? contentSnippetProp.GetString()
                        : null;

                    var isUnavailable = postId == Guid.Empty || !visiblePostIds.Contains(postId);
                    msg.PostShareInfo = new PostShareInfoModel
                    {
                        PostId = postId,
                        PostCode = postCode,
                        IsPostUnavailable = isUnavailable,
                        OwnerId = ownerId,
                        OwnerUsername = ownerUsername,
                        OwnerDisplayName = ownerDisplayName,
                        ThumbnailUrl = isUnavailable ? null : thumbnailUrl,
                        ThumbnailMediaType = isUnavailable ? null : thumbnailMediaType,
                        ContentSnippet = isUnavailable ? null : contentSnippet
                    };
                }
            }

            return (items, totalItems);
        }
    }
}
