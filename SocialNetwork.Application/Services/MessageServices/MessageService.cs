using AutoMapper;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.Services.ConversationServices;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.MessageMedias;
using SocialNetwork.Infrastructure.Repositories.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.MessageServices
{
    public class MessageService : IMessageService
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IMessageMediaRepository _messageMediaRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IConversationMemberRepository _conversationMemberRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        private readonly IConversationService _conversationService;
        public MessageService(IMessageRepository messageRepository, IMessageMediaRepository messageMediaRepository, IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IAccountRepository accountRepository1, IMapper mapper, IAccountRepository accountRepository, IConversationService conversationService)
        {
            _messageRepository = messageRepository;
            _messageMediaRepository = messageMediaRepository;
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _accountRepository = accountRepository;
            _mapper = mapper;
            _conversationService = conversationService;
        }
        public async Task<PagedResponse<MessageBasicModel>> GetMessagesByConversationIdAsync(Guid conversationId, Guid currentId, int page, int pageSize)
        {
            if(! await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
            {
                throw new ForbiddenException("You are not a member of this conversation.");
            }
            var (messages, totalItems) = await _messageRepository.GetMessagesByConversationId(conversationId, currentId, page, pageSize);
            return new PagedResponse<MessageBasicModel>
            {
                Items = messages,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
        }
        //public async Task<MessageBasicModel> SendMessageInPrivateChatAsync(Guid senderId, SendMessageInPrivateChatRequest request)
        //{
        //    if(!await _accountRepository.IsAccountIdExist(request.ReceiverId))
        //        throw new BadRequestException($"Account with ID {request.ReceiverId} does not exist.");
        //    var conversation = await _conversationRepository.GetConversationByTwoAccountIdsAsync(senderId, request.ReceiverId);
        //    bool isNewConversation = false;
        //    if (conversation == null)
        //    {
        //        conversation = await _conversationService.CreatePrivateConversationAsync(senderId, request.ReceiverId);
        //        isNewConversation = true;
        //    }
        //}
    }
}
