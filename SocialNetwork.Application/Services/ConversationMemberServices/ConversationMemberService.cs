using AutoMapper;
using SocialNetwork.Application.DTOs.ConversationMemberDTOs;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.ConversationMemberServices
{
    public class ConversationMemberService : IConversationMemberService
    {
        private readonly IConversationRepository _conversationRepository;
        private readonly IConversationMemberRepository _conversationMemberRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        public ConversationMemberService(IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IAccountRepository accountRepository1, IMapper mapper, IAccountRepository accountRepository)
        {
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _accountRepository = accountRepository;
            _mapper = mapper;
        }
        public async Task UpdateConversationMember(Guid conversationId, Guid currentId, ConversationMemberUpdateRequest request)
        {
            if(! await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
                throw new ForbiddenException("You are not a member of this conversation.");
            var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, request.AccountId);
            if (member == null)
            {
                throw new BadRequestException($"Account with ID {request.AccountId} is not a member of this conversation.");
            }
            if(request.Nickname != null)
                member.Nickname = request.Nickname.Trim();
            if (request.IsMuted.HasValue)
                member.IsMuted = request.IsMuted.Value;
        }
    }
}
