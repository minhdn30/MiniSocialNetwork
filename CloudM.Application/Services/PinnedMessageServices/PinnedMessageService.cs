using CloudM.Application.DTOs.CommonDTOs;
using AutoMapper;
using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.MessageDTOs;
using CloudM.Application.DTOs.MessageMediaDTOs;
using CloudM.Application.DTOs.PinnedMessageDTOs;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.ConversationMembers;
using CloudM.Infrastructure.Repositories.Conversations;
using CloudM.Infrastructure.Repositories.Messages;
using CloudM.Infrastructure.Repositories.PinnedMessages;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.PinnedMessageServices
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

        public PinnedMessageService(
            IPinnedMessageRepository pinnedMessageRepository,
            IMessageRepository messageRepository,
            IConversationRepository conversationRepository,
            IConversationMemberRepository conversationMemberRepository,
            IAccountRepository accountRepository,
            IRealtimeService realtimeService,
            IMapper mapper,
            IUnitOfWork unitOfWork)
        {
            _pinnedMessageRepository = pinnedMessageRepository;
            _messageRepository = messageRepository;
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _accountRepository = accountRepository;
            _realtimeService = realtimeService;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<PinnedMessageResponse>> GetPinnedMessagesAsync(Guid conversationId, Guid currentAccountId)
        {
            const int legacyBatchSize = 50;
            var firstPage = await GetPinnedMessagesAsync(
                conversationId,
                currentAccountId,
                1,
                legacyBatchSize);

            var allItems = new List<PinnedMessageResponse>();
            if (firstPage.Items != null && firstPage.Items.Any())
            {
                allItems.AddRange(firstPage.Items);
            }

            if (firstPage.TotalPages <= 1)
            {
                return allItems;
            }

            for (var page = 2; page <= firstPage.TotalPages; page++)
            {
                var pageResult = await GetPinnedMessagesAsync(
                    conversationId,
                    currentAccountId,
                    page,
                    legacyBatchSize);

                if (pageResult.Items != null && pageResult.Items.Any())
                {
                    allItems.AddRange(pageResult.Items);
                }
            }

            return allItems;
        }

        public async Task<PagedResponse<PinnedMessageResponse>> GetPinnedMessagesAsync(Guid conversationId, Guid currentAccountId, int page, int pageSize)
        {
            // verify membership
            var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentAccountId);
            if (member == null || member.HasLeft)
                throw new ForbiddenException("You are not a member of this conversation.");

            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;
            if (pageSize > 50) pageSize = 50;

            var clearedAt = member.ClearedAt;

            // query pinned messages with filters: clearedAt + hiddenBy via repository
            var (pinnedMessages, totalItems) = await _pinnedMessageRepository
                .GetPinnedMessagesByConversationIdAsync(conversationId, clearedAt, currentAccountId, page, pageSize);

            var responseItems = _mapper.Map<IReadOnlyList<PinnedMessageResponse>>(pinnedMessages);
            return new PagedResponse<PinnedMessageResponse>(responseItems, page, pageSize, totalItems);
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
                // group: only admins can pin or unpin
                var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentAccountId);
                var ownerId = conversation.Owner ?? (conversation.CreatedBy != Guid.Empty ? conversation.CreatedBy : Guid.Empty);
                var isOwner = ownerId != Guid.Empty && ownerId == currentAccountId;
                if (member == null || (!member.IsAdmin && !isOwner))
                    throw new ForbiddenException("Only group admins can pin or unpin messages.");
            }
            // 1:1 chat: anyone can pin or unpin
        }
    }
}
