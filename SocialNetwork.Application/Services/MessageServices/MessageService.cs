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

        public async Task<CursorResponse<MessageBasicModel>> GetMessagesByConversationIdAsync(Guid conversationId, Guid currentId, string? cursor, int pageSize)
        {
            if (pageSize <= 0) pageSize = 20;

            if(! await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
            {
                throw new ForbiddenException("You are not a member of this conversation.");
            }
            var (items, olderCursor, newerCursor, hasMoreOlder, hasMoreNewer) =
                await _messageRepository.GetMessagesByConversationId(conversationId, currentId, cursor, pageSize);
            return new CursorResponse<MessageBasicModel>(items, olderCursor, newerCursor, hasMoreOlder, hasMoreNewer);
        }
        public async Task<SendMessageResponse> SendMessageInPrivateChatAsync(Guid senderId, SendMessageInPrivateChatRequest request)
        {
            // === validation phase ===

            // batch query both sender and receiver in single query
            var accounts = await _accountRepository.GetAccountsByIds(new[] { senderId, request.ReceiverId })
                ?? Enumerable.Empty<Account>();
            var sender = accounts.FirstOrDefault(a => a.AccountId == senderId);
            var receiver = accounts.FirstOrDefault(a => a.AccountId == request.ReceiverId);

            // Backward-compatible fallback for repos/tests that only implement single-account lookup.
            if (sender == null)
            {
                sender = await _accountRepository.GetAccountById(senderId);
            }
            if (receiver == null)
            {
                receiver = await _accountRepository.GetAccountById(request.ReceiverId);
            }
            
            if (receiver == null)
                throw new BadRequestException($"Receiver account with ID {request.ReceiverId} does not exist.");
            if (receiver.Status != AccountStatusEnum.Active)
                throw new BadRequestException("This user is currently unavailable.");

            if(sender == null) 
                throw new BadRequestException($"Sender account with ID {senderId} does not exist.");
            if (sender.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to send messages.");
            
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
                        case MediaTypeEnum.Document:
                            url = await _cloudinaryService.UploadRawFileAsync(file);
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

                    // Validate reply target
                    ReplyInfoModel? replyInfo = null;
                    if (request.ReplyToMessageId.HasValue)
                    {
                        var replyTarget = await _messageRepository.GetMessageByIdAsync(request.ReplyToMessageId.Value);
                        if (replyTarget == null)
                            throw new BadRequestException("Reply target message not found.");
                        if (replyTarget.ConversationId != actualConversationId)
                            throw new BadRequestException("Reply target message does not belong to this conversation.");
                        if (replyTarget.MessageType == MessageTypeEnum.System)
                            throw new BadRequestException("Cannot reply to a system message.");

                        var replySenderMember = await _conversationMemberRepository
                            .GetConversationMemberAsync(actualConversationId, replyTarget.AccountId);

                        replyInfo = new ReplyInfoModel
                        {
                            MessageId = replyTarget.MessageId,
                            Content = replyTarget.IsRecalled ? null : replyTarget.Content,
                            IsRecalled = replyTarget.IsRecalled,
                            IsHidden = false,
                            MessageType = replyTarget.MessageType,
                            ReplySenderId = replyTarget.AccountId,
                            Sender = new ReplySenderInfoModel
                            {
                                Username = replyTarget.Account?.Username ?? "",
                                DisplayName = replySenderMember?.Nickname ?? replyTarget.Account?.Username ?? ""
                            }
                        };
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
                        IsRecalled = false,
                        ReplyToMessageId = request.ReplyToMessageId
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
                    result.ReplyTo = replyInfo;
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
                        case MediaTypeEnum.Document:
                            url = await _cloudinaryService.UploadRawFileAsync(file);
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
                    // Validate reply target
                    ReplyInfoModel? replyInfo = null;
                    if (request.ReplyToMessageId.HasValue)
                    {
                        var replyTarget = await _messageRepository.GetMessageByIdAsync(request.ReplyToMessageId.Value);
                        if (replyTarget == null)
                            throw new BadRequestException("Reply target message not found.");
                        if (replyTarget.ConversationId != conversationId)
                            throw new BadRequestException("Reply target message does not belong to this conversation.");
                        if (replyTarget.MessageType == MessageTypeEnum.System)
                            throw new BadRequestException("Cannot reply to a system message.");

                        var replySenderMember = await _conversationMemberRepository
                            .GetConversationMemberAsync(conversationId, replyTarget.AccountId);

                        replyInfo = new ReplyInfoModel
                        {
                            MessageId = replyTarget.MessageId,
                            Content = replyTarget.IsRecalled ? null : replyTarget.Content,
                            IsRecalled = replyTarget.IsRecalled,
                            IsHidden = false,
                            MessageType = replyTarget.MessageType,
                            ReplySenderId = replyTarget.AccountId,
                            Sender = new ReplySenderInfoModel
                            {
                                Username = replyTarget.Account?.Username ?? "",
                                DisplayName = replySenderMember?.Nickname ?? replyTarget.Account?.Username ?? ""
                            }
                        };
                    }

                    // create message in existing conversation
                    var message = new Message
                    {
                        ConversationId = conversationId,
                        AccountId = senderId,
                        Content = request.Content,
                        MessageType = mediaEntities.Any() ? MessageTypeEnum.Media : MessageTypeEnum.Text,
                        SentAt = now,
                        IsEdited = false,
                        IsRecalled = false,
                        ReplyToMessageId = request.ReplyToMessageId
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
                    result.ReplyTo = replyInfo;
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

        public async Task<string> GetMediaDownloadUrlAsync(Guid messageMediaId, Guid accountId)
        {
            var media = await _messageMediaRepository.GetByIdWithMessageAsync(messageMediaId);
            if (media == null)
                throw new NotFoundException("Attachment not found.");

            var conversationId = media.Message?.ConversationId ?? Guid.Empty;
            if (conversationId == Guid.Empty)
                throw new NotFoundException("Conversation not found.");

            var isMember = await _conversationMemberRepository.IsMemberOfConversation(conversationId, accountId);
            if (!isMember)
                throw new ForbiddenException("You are not allowed to access this attachment.");

            var signedUrl = _cloudinaryService.GetDownloadUrl(media.MediaUrl, media.MediaType, media.FileName);
            if (string.IsNullOrWhiteSpace(signedUrl))
                throw new BadRequestException("Could not generate attachment download URL.");

            return signedUrl;
        }

        public async Task<RecallMessageResponse> RecallMessageAsync(Guid messageId, Guid currentId)
        {
            var message = await _messageRepository.GetMessageByIdAsync(messageId);
            if (message == null)
                throw new NotFoundException("Message not found.");

            if (message.AccountId != currentId)
                throw new ForbiddenException("You can only recall your own messages.");

            if (message.IsRecalled)
            {
                return new RecallMessageResponse
                {
                    MessageId = message.MessageId,
                    ConversationId = message.ConversationId,
                    RecalledAt = message.RecalledAt ?? DateTime.UtcNow
                };
            }

            message.IsRecalled = true;
            message.RecalledAt = DateTime.UtcNow;

            await _unitOfWork.CommitAsync();

            await _realtimeService.NotifyMessageRecalledAsync(
                message.ConversationId,
                message.MessageId,
                currentId,
                message.RecalledAt.Value);

            return new RecallMessageResponse
            {
                MessageId = message.MessageId,
                ConversationId = message.ConversationId,
                RecalledAt = message.RecalledAt.Value
            };
        }

    }
}
