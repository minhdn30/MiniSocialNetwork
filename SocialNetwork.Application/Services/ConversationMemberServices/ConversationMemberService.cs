using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.ConversationMemberDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.Services.RealtimeServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.Messages;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.ConversationMemberServices
{
    public class ConversationMemberService : IConversationMemberService
    {
        private readonly IConversationRepository _conversationRepository;
        private readonly IConversationMemberRepository _conversationMemberRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IRealtimeService _realtimeService;
        public ConversationMemberService(IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IAccountRepository accountRepository1, IMapper mapper, IAccountRepository accountRepository, IMessageRepository messageRepository,
            IUnitOfWork unitOfWork, IRealtimeService realtimeService)
        {
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _accountRepository = accountRepository;
            _messageRepository = messageRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _realtimeService = realtimeService;
        }
        public async Task SetMuteStatusAsync(Guid conversationId, Guid currentId, bool isMuted)
        {
            var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (member == null)
                throw new ForbiddenException($"Account with ID {currentId} is not a member of this conversation.");

            if (member.IsMuted != isMuted)
            {
                member.IsMuted = isMuted;
                await _conversationMemberRepository.UpdateConversationMember(member);
                await _unitOfWork.CommitAsync();
            }

            await _realtimeService.NotifyConversationMuteUpdatedAsync(currentId, conversationId, isMuted);
        }
        public async Task UpdateMemberNickname(Guid conversationId, Guid currentId, ConversationMemberNicknameUpdateRequest request)
        {
            if(! await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
                throw new ForbiddenException("You are not a member of this conversation.");

            var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, request.AccountId);
            if (member == null)
                throw new BadRequestException($"Account with ID {request.AccountId} is not a member of this conversation.");

            var actor = await _accountRepository.GetAccountById(currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var target = request.AccountId == currentId ? actor : await _accountRepository.GetAccountById(request.AccountId);
            if (target == null)
                throw new NotFoundException($"Account with ID {request.AccountId} does not exist.");

            var oldNickname = member.Nickname;
            var trimmed = request.Nickname?.Trim();
            member.Nickname = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;

            var nicknameChanged = !string.Equals(oldNickname, member.Nickname, StringComparison.Ordinal);
            if (!nicknameChanged)
                return;

            await _conversationMemberRepository.UpdateConversationMember(member);
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildNicknameSystemMessageFallback(actor.Username, target.Username, member.Nickname),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.MemberNicknameUpdated,
                    actorAccountId = currentId,
                    actorUsername = actor.Username,
                    targetAccountId = request.AccountId,
                    targetUsername = target.Username,
                    nickname = member.Nickname,
                    previousNickname = oldNickname
                }),
                SentAt = DateTime.UtcNow,
                IsEdited = false,
                IsRecalled = false
            };
            await _messageRepository.AddMessageAsync(systemMessage);
            await _unitOfWork.CommitAsync();

            var memberIds = await _conversationMemberRepository.GetMemberIdsByConversationIdAsync(conversationId);
            await _realtimeService.NotifyConversationNicknameUpdatedAsync(
                conversationId,
                request.AccountId,
                member.Nickname,
                currentId,
                memberIds);

            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }
        public async Task<bool> IsMemberAsync(Guid conversationId, Guid accountId)
        {
            return await _conversationMemberRepository.IsMemberOfConversation(conversationId, accountId);
        }
        public async Task SoftDeleteChatHistory(Guid conversationId, Guid currentId)
        {
            var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (member == null)
                throw new ForbiddenException($"Account with ID {currentId} is not a member of this conversation.");
            member.ClearedAt = DateTime.UtcNow;
            await _conversationMemberRepository.UpdateConversationMember(member);
            await _unitOfWork.CommitAsync();
            await _realtimeService.NotifyConversationRemovedAsync(currentId, conversationId, "soft-delete");
        }
        public async Task MarkSeenAsync(Guid conversationId, Guid currentId, Guid newMessageId)
        {
            var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (member == null)
                throw new ForbiddenException($"Account with ID {currentId} is not a member of this conversation.");
            if(member.LastSeenMessageId == null || await _messageRepository.IsMessageNewer(newMessageId, member.LastSeenMessageId.Value))
            {
                member.LastSeenMessageId = newMessageId;
                member.LastSeenAt = DateTime.UtcNow;
                await _conversationMemberRepository.UpdateConversationMember(member);
                await _unitOfWork.CommitAsync();
            }        
        }

        private static string BuildNicknameSystemMessageFallback(string actorUsername, string targetUsername, string? nickname)
        {
            var actorMention = ToMention(actorUsername);
            var targetMention = ToMention(targetUsername);

            if (string.IsNullOrWhiteSpace(nickname))
            {
                return $"{actorMention} removed nickname for {targetMention}.";
            }

            return $"{actorMention} set nickname for {targetMention} to \"{nickname}\".";
        }

        private static string ToMention(string? username)
        {
            var normalized = (username ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "@unknown";
            }

            return normalized.StartsWith("@", StringComparison.Ordinal)
                ? normalized
                : $"@{normalized}";
        }
    }
}
