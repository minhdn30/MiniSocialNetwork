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
            IAccountRepository accountRepository, IMapper mapper, ICloudinaryService cloudinaryService,
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
            // === validation phase ===
            
            // check can't send to self
            if(senderId == request.ReceiverId)
                throw new BadRequestException("You cannot send a message to yourself.");

            // check content + media not both empty
            if(string.IsNullOrWhiteSpace(request.Content) && (request.MediaFiles == null || !request.MediaFiles.Any()))
                throw new BadRequestException("Message content and media files cannot both be empty.");

            // batch query both sender and receiver in single query
            var accounts = await _accountRepository.GetAccountsByIds(new[] { senderId, request.ReceiverId });
            var sender = accounts.FirstOrDefault(a => a.AccountId == senderId);
            var receiver = accounts.FirstOrDefault(a => a.AccountId == request.ReceiverId);
            
            if(sender == null) 
                throw new BadRequestException($"Sender account with ID {senderId} does not exist.");
            if (sender.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to send messages.");
            
            if (receiver == null)
                throw new BadRequestException($"Receiver account with ID {request.ReceiverId} does not exist.");
            if (receiver.Status != AccountStatusEnum.Active)
                throw new BadRequestException("This user is currently unavailable.");
            
            var now = DateTime.UtcNow;

            // === media upload phase (before transaction) ===, track uploaded URLs for cleanup) ===
            var uploadedMedia = new List<(string url, MediaTypeEnum type)>();
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
                    
                    if (!string.IsNullOrEmpty(url))
                    {
                        uploadedMedia.Add((url, detectedType.Value));
                        mediaEntities.Add(new MessageMedia
                        {
                            MediaUrl = url,
                            MediaType = detectedType.Value,
                            FileName = file.FileName,
                            FileSize = file.Length,
                            CreatedAt = now
                        });
                    }
                }
            }
            
            // === database transaction phase ===
            return await _unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    // get or create conversation - optimized to only fetch id
                    var conversationId = await _conversationRepository.GetPrivateConversationIdAsync(senderId, request.ReceiverId);
                    Guid actualConversationId;
                    if (conversationId == null)
                    {
                        var conversation = await _conversationRepository.CreatePrivateConversationAsync(senderId, request.ReceiverId);
                        // Flush conversation + members to DB first (still within the transaction)
                        // This ensures the FK reference exists before inserting the message
                        await _unitOfWork.CommitAsync();
                        actualConversationId = conversation.ConversationId;
                    }
                    else
                    {
                        actualConversationId = conversationId.Value;
                    }

                    // Create message
                    var message = new Message
                    {
                        ConversationId = actualConversationId,
                        AccountId = senderId,
                        Content = request.Content,
                        MessageType = mediaEntities.Any() ? MessageTypeEnum.Media : MessageTypeEnum.Text,
                        SentAt = now,
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
                    result.TempId = request.TempId;
                    result.Sender = _mapper.Map<AccountChatInfoResponse>(sender);
                    if (mediaEntities.Any())
                    {
                        result.Medias = _mapper.Map<List<MessageMediaResponse>>(mediaEntities);
                    }

                    // Send realtime notification (after successful commit)
                    var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(result.ConversationId);
                    await _realtimeService.NotifyNewMessageAsync(result.ConversationId, muteMap, result);

                    return result;
                },
                // Cleanup callback: delete orphaned cloud media if DB transaction fails
                async () =>
                {
                    var cleanupTasks = uploadedMedia.Select(m =>
                    {
                        var publicId = _cloudinaryService.GetPublicIdFromUrl(m.url);
                        return !string.IsNullOrEmpty(publicId) 
                            ? _cloudinaryService.DeleteMediaAsync(publicId, m.type)
                            : Task.CompletedTask;
                    });
                    await Task.WhenAll(cleanupTasks);
                }
            );
        }

        // send message to group chat (conversation must exist)
        public async Task<SendMessageResponse> SendMessageInGroupAsync(Guid senderId, Guid conversationId, SendMessageRequest request)
        {
            // === validation phase ===
            
            // check content + media not both empty
            if(string.IsNullOrWhiteSpace(request.Content) && (request.MediaFiles == null || !request.MediaFiles.Any()))
                throw new BadRequestException("Message content and media files cannot both be empty.");
            
            // verify conversation exists
            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException("Conversation not found.");
            
            // verify it's a group conversation
            if (!conversation.IsGroup)
                throw new BadRequestException("This endpoint is for group chats only. Use /private-chat for 1:1 messaging.");
            
            // verify user is member of this conversation
            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, senderId))
                throw new ForbiddenException("You are not a member of this conversation.");
            
            // verify sender exists and active
            var sender = await _accountRepository.GetAccountById(senderId);
            if(sender == null) 
                throw new BadRequestException($"Sender account with ID {senderId} does not exist.");
            if (sender.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to send messages.");
            
            var now = DateTime.UtcNow;

            // === media upload phase (before transaction) ===
            var uploadedMedia = new List<(string url, MediaTypeEnum type)>();
            var mediaEntities = new List<MessageMedia>();
            
            if (request.MediaFiles != null && request.MediaFiles.Any())
            {
                foreach (var file in request.MediaFiles)
                {
                    var detectedType = await _fileTypeDetector.GetMediaTypeAsync(file);
                    if (detectedType == null)
                        continue;
                    
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
                    
                    if (!string.IsNullOrEmpty(url))
                    {
                        uploadedMedia.Add((url, detectedType.Value));
                        mediaEntities.Add(new MessageMedia
                        {
                            MediaUrl = url,
                            MediaType = detectedType.Value,
                            FileName = file.FileName,
                            FileSize = file.Length,
                            CreatedAt = now
                        });
                    }
                }
            }
            
            // === database transaction phase ===
            return await _unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    // create message in existing conversation
                    var message = new Message
                    {
                        ConversationId = conversationId,
                        AccountId = senderId,
                        Content = request.Content,
                        MessageType = mediaEntities.Any() ? MessageTypeEnum.Media : MessageTypeEnum.Text,
                        SentAt = now,
                        IsEdited = false,
                        IsRecalled = false
                    };
                    await _messageRepository.AddMessageAsync(message);
                    
                    // link media to message
                    if (mediaEntities.Any())
                    {
                        foreach (var media in mediaEntities)
                        {
                            media.MessageId = message.MessageId;
                        }
                        await _messageMediaRepository.AddMessageMediasAsync(mediaEntities);
                    }
                    
                    // build response
                    var result = _mapper.Map<SendMessageResponse>(message);
                    result.TempId = request.TempId;
                    result.Sender = _mapper.Map<AccountChatInfoResponse>(sender);
                    if (mediaEntities.Any())
                    {
                        result.Medias = _mapper.Map<List<MessageMediaResponse>>(mediaEntities);
                    }
                    
                    // send realtime notification to conversation members
                    var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(result.ConversationId);
                    await _realtimeService.NotifyNewMessageAsync(result.ConversationId, muteMap, result);
                    
                    return result;
                },
                // cleanup callback: delete orphaned cloud media if db transaction fails
                async () =>
                {
                    var cleanupTasks = uploadedMedia.Select(m =>
                    {
                        var publicId = _cloudinaryService.GetPublicIdFromUrl(m.url);
                        return !string.IsNullOrEmpty(publicId) 
                            ? _cloudinaryService.DeleteMediaAsync(publicId, m.type)
                            : Task.CompletedTask;
                    });
                    await Task.WhenAll(cleanupTasks);
                }
            );
        }

    }
}
