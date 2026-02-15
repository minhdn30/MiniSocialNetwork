using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.DTOs.MessageMediaDTOs;
using SocialNetwork.Application.DTOs.PinnedMessageDTOs;
using SocialNetwork.Application.Services.RealtimeServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.Messages;
using SocialNetwork.Infrastructure.Repositories.PinnedMessages;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.PinnedMessageServices
{
    public class PinnedMessageService : IPinnedMessageService
    {
        private readonly IPinnedMessageRepository _pinnedMessageRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IConversationMemberRepository _conversationMemberRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IRealtimeService _realtimeService;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly AppDbContext _context;

        public PinnedMessageService(
            IPinnedMessageRepository pinnedMessageRepository,
            IMessageRepository messageRepository,
            IConversationRepository conversationRepository,
            IConversationMemberRepository conversationMemberRepository,
            IAccountRepository accountRepository,
            IRealtimeService realtimeService,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            AppDbContext context)
        {
            _pinnedMessageRepository = pinnedMessageRepository;
            _messageRepository = messageRepository;
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _accountRepository = accountRepository;
            _realtimeService = realtimeService;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _context = context;
        }

        public async Task<IEnumerable<PinnedMessageResponse>> GetPinnedMessagesAsync(Guid conversationId, Guid currentAccountId)
        {
            // verify membership
            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentAccountId))
                throw new ForbiddenException("You are not a member of this conversation.");

            // get ClearedAt for this user
            var member = await _context.ConversationMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.ConversationId == conversationId &&
                            cm.AccountId == currentAccountId &&
                            !cm.HasLeft);
            var clearedAt = member?.ClearedAt;

            // query pinned messages with filters: ClearedAt + HiddenBy
            var pinnedMessages = await _context.PinnedMessages
                .AsNoTracking()
                .Where(pm => pm.ConversationId == conversationId)
                // filter ClearedAt (personal)
                .Where(pm => clearedAt == null || pm.Message.SentAt >= clearedAt)
                // filter HiddenBy (personal)
                .Where(pm => !pm.Message.HiddenBy.Any(hb => hb.AccountId == currentAccountId))
                .OrderByDescending(pm => pm.PinnedAt)
                .Select(pm => new PinnedMessageResponse
                {
                    MessageId = pm.MessageId,
                    ConversationId = pm.ConversationId,
                    // if recalled, hide content
                    Content = pm.Message.IsRecalled ? null : pm.Message.Content,
                    MessageType = pm.Message.MessageType,
                    SentAt = pm.Message.SentAt,
                    IsRecalled = pm.Message.IsRecalled,

                    // original sender
                    Sender = new AccountChatInfoResponse
                    {
                        AccountId = pm.Message.Account.AccountId,
                        Username = pm.Message.Account.Username,
                        FullName = pm.Message.Account.FullName,
                        AvatarUrl = pm.Message.Account.AvatarUrl,
                    },

                    // media (null if recalled)
                    Medias = pm.Message.IsRecalled ? null : pm.Message.Medias
                        .OrderBy(mm => mm.CreatedAt)
                        .Select(mm => new MessageMediaResponse
                        {
                            MessageMediaId = mm.MessageMediaId,
                            MediaUrl = mm.MediaUrl,
                            ThumbnailUrl = mm.ThumbnailUrl,
                            MediaType = mm.MediaType,
                            FileName = mm.FileName,
                            FileSize = mm.FileSize,
                        })
                        .ToList(),

                    // pin metadata
                    PinnedByAccount = new AccountChatInfoResponse
                    {
                        AccountId = pm.PinnedByAccount.AccountId,
                        Username = pm.PinnedByAccount.Username,
                        FullName = pm.PinnedByAccount.FullName,
                        AvatarUrl = pm.PinnedByAccount.AvatarUrl,
                    },
                    PinnedAt = pm.PinnedAt,
                })
                .ToListAsync();

            return pinnedMessages;
        }

        public async Task PinMessageAsync(Guid conversationId, Guid messageId, Guid currentAccountId)
        {
            // verify membership
            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentAccountId))
                throw new ForbiddenException("You are not a member of this conversation.");

            // verify message exists and belongs to this conversation
            var message = await _messageRepository.GetMessageByIdAsync(messageId);
            if (message == null)
                throw new NotFoundException("Message not found.");
            if (message.ConversationId != conversationId)
                throw new BadRequestException("Message does not belong to this conversation.");

            // cannot pin system messages
            if (message.MessageType == MessageTypeEnum.System)
                throw new BadRequestException("System messages cannot be pinned.");

            // authorization: group = admin only, 1:1 = anyone
            await EnsurePinPermission(conversationId, currentAccountId);

            // check if already pinned
            if (await _pinnedMessageRepository.IsPinnedAsync(conversationId, messageId))
                throw new BadRequestException("Message is already pinned.");

            // get actor info
            var actor = await _accountRepository.GetAccountById(currentAccountId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentAccountId} does not exist.");

            // pin the message
            var pinnedMessage = new PinnedMessage
            {
                ConversationId = conversationId,
                MessageId = messageId,
                PinnedBy = currentAccountId,
                PinnedAt = DateTime.UtcNow
            };
            await _pinnedMessageRepository.AddAsync(pinnedMessage);

            // create system message
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentAccountId,
                MessageType = MessageTypeEnum.System,
                Content = $"@{actor.Username} pinned a message.",
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.MessagePinned,
                    actorAccountId = currentAccountId,
                    actorUsername = actor.Username,
                    pinnedMessageId = messageId
                }),
                SentAt = DateTime.UtcNow,
                IsEdited = false,
                IsRecalled = false
            };
            await _messageRepository.AddMessageAsync(systemMessage);

            await _unitOfWork.CommitAsync();

            // broadcast system message via realtime
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        public async Task UnpinMessageAsync(Guid conversationId, Guid messageId, Guid currentAccountId)
        {
            // verify membership
            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentAccountId))
                throw new ForbiddenException("You are not a member of this conversation.");

            // check if pinned
            if (!await _pinnedMessageRepository.IsPinnedAsync(conversationId, messageId))
                throw new BadRequestException("Message is not pinned.");

            // authorization: group = admin only, 1:1 = anyone
            await EnsurePinPermission(conversationId, currentAccountId);

            // get actor info
            var actor = await _accountRepository.GetAccountById(currentAccountId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentAccountId} does not exist.");

            // unpin the message
            await _pinnedMessageRepository.RemoveAsync(conversationId, messageId);

            // create system message
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentAccountId,
                MessageType = MessageTypeEnum.System,
                Content = $"@{actor.Username} unpinned a message.",
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.MessageUnpinned,
                    actorAccountId = currentAccountId,
                    actorUsername = actor.Username,
                    unpinnedMessageId = messageId
                }),
                SentAt = DateTime.UtcNow,
                IsEdited = false,
                IsRecalled = false
            };
            await _messageRepository.AddMessageAsync(systemMessage);

            await _unitOfWork.CommitAsync();

            // broadcast system message via realtime
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        private async Task EnsurePinPermission(Guid conversationId, Guid currentAccountId)
        {
            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException("Conversation not found.");

            if (conversation.IsGroup)
            {
                // group: only admins can pin/unpin
                var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentAccountId);
                if (member == null || !member.IsAdmin)
                    throw new ForbiddenException("Only group admins can pin or unpin messages.");
            }
            // 1:1 chat: anyone can pin/unpin (no check needed)
        }
    }
}
