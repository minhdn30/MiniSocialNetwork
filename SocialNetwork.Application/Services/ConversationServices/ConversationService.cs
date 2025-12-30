using AutoMapper;
using CloudinaryDotNet;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.Follows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.ConversationServices
{
    public class ConversationService : IConversationService
    {
        private readonly IConversationRepository _conversationRepository;
        private readonly IConversationMemberRepository _conversationMemberRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        public ConversationService(IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IAccountRepository accountRepository1, IMapper mapper, IAccountRepository accountRepository)
        {
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _accountRepository = accountRepository;
            _mapper = mapper;
        }
        public async Task<ConversationResponse?> GetPrivateConversationAsync(Guid currentId, Guid otherId)
        {
            if (currentId == otherId)
                throw new BadRequestException("Sender and receiver cannot be the same.");          
            var conversation = await _conversationRepository.GetConversationByTwoAccountIdsAsync(currentId, otherId);
            return conversation == null ? null : _mapper.Map<ConversationResponse>(conversation);         
        }
        public async Task<ConversationResponse> CreatePrivateConversationAsync(Guid currentId, Guid otherId)
        {
            if (currentId == otherId)
                throw new BadRequestException("Sender and receiver cannot be the same.");
            if(!await _accountRepository.IsAccountIdExist(currentId) || !await _accountRepository.IsAccountIdExist(otherId))
                throw new NotFoundException("One or both accounts do not exist.");
            if(await _conversationRepository.IsPrivateConversationExistBetweenTwoAccounts(currentId, otherId))
                throw new BadRequestException("A private conversation between these two accounts already exists.");
            var conversation = await _conversationRepository.CreatePrivateConversationAsync(currentId, otherId);
            return _mapper.Map<ConversationResponse>(conversation);
        }
    }
}
