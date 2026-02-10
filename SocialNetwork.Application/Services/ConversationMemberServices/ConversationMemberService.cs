using AutoMapper;
using SocialNetwork.Application.DTOs.ConversationMemberDTOs;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public ConversationMemberService(IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IAccountRepository accountRepository1, IMapper mapper, IAccountRepository accountRepository, IMessageRepository messageRepository,
            IUnitOfWork unitOfWork)
        {
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _accountRepository = accountRepository;
            _messageRepository = messageRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
        public async Task UpdateMemberNickname(Guid conversationId, Guid currentId, ConversationMemberNicknameUpdateRequest request)
        {
            if(! await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
                throw new ForbiddenException("You are not a member of this conversation.");
            var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, request.AccountId);
            if (member == null)
                throw new BadRequestException($"Account with ID {request.AccountId} is not a member of this conversation.");
            if(request.Nickname != null)
                member.Nickname = request.Nickname.Trim();
            await _conversationMemberRepository.UpdateConversationMember(member);
            await _unitOfWork.CommitAsync();
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
    }
}
