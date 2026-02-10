using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.DTOs.MessageMediaDTOs;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using SocialNetwork.Application.Services.ConversationServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.MessageMedias;
using SocialNetwork.Infrastructure.Repositories.Messages;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Application.Services.RealtimeServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

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
        private readonly IRealtimeService _realtimeService;
        private readonly IUnitOfWork _unitOfWork;

        public MessageService(IMessageRepository messageRepository, IMessageMediaRepository messageMediaRepository, IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IAccountRepository accountRepository1, IMapper mapper, IAccountRepository accountRepository, ICloudinaryService cloudinaryService,
            IFileTypeDetector fileTypeDetector, IRealtimeService realtimeService, IUnitOfWork unitOfWork)
        {
            _messageRepository = messageRepository;
            _messageMediaRepository = messageMediaRepository;
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _accountRepository = accountRepository;
            _cloudinaryService = cloudinaryService;
            _fileTypeDetector = fileTypeDetector;
            _mapper = mapper;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
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
            // === VALIDATION PHASE ===
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

            // === MEDIA UPLOAD PHASE (before transaction, track uploaded URLs for cleanup) ===
            var uploadedMediaUrls = new List<string>();
            var mediaEntities = new List<MessageMedia>();
            
            if (request.MediaFiles != null && request.MediaFiles.Any())
            {
                foreach (var file in request.MediaFiles)
                {
                    var detectedType = await _fileTypeDetector.GetMediaTypeAsync(file);
                    if (detectedType == null) continue;
                    
                    string? url = null;
                    switch(detectedType.Value)
                    {
                        case MediaTypeEnum.Image:
                            url = await _cloudinaryService.UploadImageAsync(file);
                            break;
                        case MediaTypeEnum.Video:
                            url = await _cloudinaryService.UploadVideoAsync(file);
                            break;
                        default:
                            continue; 
                    };
                    
                    if(!string.IsNullOrEmpty(url))
                    {
                        uploadedMediaUrls.Add(url);
                        mediaEntities.Add(new MessageMedia
                        {
                            MediaUrl = url,
                            MediaType = detectedType.Value,
                            FileName = file.FileName,
                            FileSize = file.Length,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            // === DATABASE TRANSACTION PHASE ===
            return await _unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    // Get or create conversation
                    var conversation = await _conversationRepository.GetConversationByTwoAccountIdsAsync(senderId, request.ReceiverId);
                    if (conversation == null)
                    {
                        conversation = await _conversationRepository.CreatePrivateConversationAsync(senderId, request.ReceiverId);
                    }

                    // Create message
                    var message = new Message
                    {
                        ConversationId = conversation.ConversationId,
                        AccountId = senderId,
                        Content = request.Content,
                        MessageType = mediaEntities.Any() ? MessageTypeEnum.Media : MessageTypeEnum.Text,
                        SentAt = DateTime.UtcNow,
                        IsEdited = false,
                        IsRecalled = false
                    };
                    await _messageRepository.AddMessageAsync(message);

                    // Link media to message and save
                    if (mediaEntities.Any())
                    {
                        foreach (var media in mediaEntities)
                        {
                            media.MessageId = message.MessageId;
                        }
                        await _messageMediaRepository.AddMessageMediasAsync(mediaEntities);
                    }

                    // Build response
                    var result = _mapper.Map<SendMessageResponse>(message);
                    result.Sender = _mapper.Map<AccountChatInfoResponse>(sender);
                    if (mediaEntities.Any())
                    {
                        result.Medias = _mapper.Map<List<MessageMediaResponse>>(mediaEntities);
                    }

                    // Send realtime notification (after successful commit)
                    await _realtimeService.NotifyNewMessageAsync(result.ConversationId, result);

                    return result;
                },
                // Cleanup callback: delete orphaned cloud media if DB transaction fails
                async () =>
                {
                    var cleanupTasks = uploadedMediaUrls.Select(url =>
                    {
                        var publicId = _cloudinaryService.GetPublicIdFromUrl(url);
                        return !string.IsNullOrEmpty(publicId) 
                            ? _cloudinaryService.DeleteMediaAsync(publicId, MediaTypeEnum.Image) 
                            : Task.CompletedTask;
                    });
                    await Task.WhenAll(cleanupTasks);
                }
            );
        }

    }
}
