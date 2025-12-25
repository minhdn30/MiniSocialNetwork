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
        public async Task<ConversationResponse> GetOrCreateConversationAsync(Guid senderId, Guid receiverId)
        {
            if(senderId == receiverId)
            {
                throw new BadRequestException("Sender and receiver cannot be the same.");
            }
            if(!await _accountRepository.IsAccountIdExist(senderId) || !await _accountRepository.IsAccountIdExist(receiverId))
            {
                throw new BadRequestException("One or both account IDs do not exist.");
            }
            var conversation = await _conversationRepository.GetConversationByTwoAccountIdsAsync(senderId, receiverId);
            if (conversation != null)
                return _mapper.Map<ConversationResponse>(conversation);
            //esle -> create new conversation
            conversation = new Conversation
            {
                ConversationId = Guid.NewGuid(),
                IsGroup = false,
                CreatedBy = senderId,
            };
            await _conversationRepository.AddConversationAsync(conversation);
            var members = new List<ConversationMember>
                {
                    new ConversationMember
                    {
                        ConversationId = conversation.ConversationId,
                        AccountId = senderId,
                        ClearedAt = null,
                        IsDeleted = false
                        
                    },
                    new ConversationMember
                    {
                        ConversationId = conversation.ConversationId,
                        AccountId = receiverId,
                        ClearedAt = null,
                        IsDeleted = false

                    }
                };
            await _conversationMemberRepository.AddConversationMembers(members);
            var result = _mapper.Map<ConversationResponse>(conversation);
            return result;

        }
    }
}
