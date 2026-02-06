using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.DTOs.MessageMediaDTOs;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Application.Services.CloudinaryServices;
using SocialNetwork.Application.Services.ConversationServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
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
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IFileTypeDetector _fileTypeDetector;
        private readonly IMapper _mapper;
        public MessageService(IMessageRepository messageRepository, IMessageMediaRepository messageMediaRepository, IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IAccountRepository accountRepository1, IMapper mapper, IAccountRepository accountRepository, ICloudinaryService cloudinaryService,
            IFileTypeDetector fileTypeDetector)
        {
            _messageRepository = messageRepository;
            _messageMediaRepository = messageMediaRepository;
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _accountRepository = accountRepository;
            _cloudinaryService = cloudinaryService;
            _fileTypeDetector = fileTypeDetector;
            _mapper = mapper;
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
        public async Task<SendMessageResponse> SendMessageInPrivateChatAsync(Guid senderId, SendMessageInPrivateChatRequest request)
        {
            if(senderId == request.ReceiverId)
                throw new BadRequestException("You cannot send a message to yourself.");
            if(string.IsNullOrWhiteSpace(request.Content) && (request.MediaFiles == null || !request.MediaFiles.Any()))
                throw new BadRequestException("Message content and media files cannot both be empty.");
            var receiver = await _accountRepository.GetAccountById(request.ReceiverId);
            if (receiver == null)
                throw new BadRequestException($"Receiver account with ID {request.ReceiverId} does not exist.");
            
            if (receiver.Status != AccountStatusEnum.Active)
                throw new BadRequestException("This user is currently unavailable.");

            var sender = await _accountRepository.GetAccountById(senderId);
            if(sender == null) 
                throw new BadRequestException($"Sender account with ID {senderId} does not exist.");

            if (sender.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to send messages.");
            var conversation = await _conversationRepository.GetConversationByTwoAccountIdsAsync(senderId, request.ReceiverId);
            if (conversation == null)
            {
                conversation = await _conversationRepository.CreatePrivateConversationAsync(senderId, request.ReceiverId);
            }
            var message = new Message
            {
                ConversationId = conversation.ConversationId,
                AccountId = senderId,
                Content = request.Content,
                MessageType = request.MediaFiles?.Any() == true ? MessageTypeEnum.Media : MessageTypeEnum.Text,
                SentAt = DateTime.UtcNow,
                IsEdited = false,
                IsDeleted = false
            };
            await _messageRepository.AddMessageAsync(message);
            var result = _mapper.Map<SendMessageResponse>(message);
            result.Sender = _mapper.Map<AccountBasicInfoResponse>(sender);
            if (request.MediaFiles != null && request.MediaFiles.Any())
            {
                var medias = new List<MessageMedia>();
                foreach (var file in request.MediaFiles)
                {
                    var detectedType = await _fileTypeDetector.GetMediaTypeAsync(file);
                    if (detectedType == null) continue; // Skip unsupported media types
                    string? url = null;
                    switch(detectedType.Value)
                    {
                        case MediaTypeEnum.Image:
                            url = await _cloudinaryService.UploadImageAsync(file);
                            break;
                        case MediaTypeEnum.Video:
                            url = await _cloudinaryService.UploadVideoAsync(file);
                            break;
                        //Audio and Document upload later
                        default:
                            continue; 
                    };
                    if(!string.IsNullOrEmpty(url))
                    {
                        medias.Add(new MessageMedia
                        {
                            MessageId = message.MessageId,
                            MediaUrl = url,
                            MediaType = detectedType.Value,
                            FileName = file.FileName,
                            FileSize = file.Length,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                if(medias.Any())
                {
                    await _messageMediaRepository.AddMessageMediasAsync(medias);
                    result.Medias = _mapper.Map<List<MessageMediaResponse>>(medias);
                }
            }
            return result;
        }
    }
}
