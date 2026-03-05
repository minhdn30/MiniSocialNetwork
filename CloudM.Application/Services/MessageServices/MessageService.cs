using AutoMapper;
using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.MessageDTOs;
using CloudM.Application.DTOs.MessageMediaDTOs;
using CloudM.Application.Helpers.FileTypeHelpers;
using CloudM.Application.Helpers.ValidationHelpers;
using CloudM.Infrastructure.Services.Cloudinary;
using CloudM.Application.Services.ConversationServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.ConversationMembers;
using CloudM.Infrastructure.Repositories.Conversations;
using CloudM.Infrastructure.Repositories.MessageMedias;
using CloudM.Infrastructure.Repositories.Messages;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.Stories;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Application.Services.RealtimeServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.MessageServices
{
    public class MessageService : IMessageService
    {
        private const int MaxPostShareRecipientsPerRequest = 50;
        private const int DefaultPostShareSearchLimit = 20;
        private const int MaxPostShareSearchLimit = 50;
        private const int EmptyKeywordRecentContactsLimit = 10;
        private const int PostShareSearchPrefetchMultiplier = 6;
        private const int MaxPostShareSearchPrefetch = 300;
        private const int MaxForwardRecipientsPerRequest = 50;
        private readonly IMessageRepository _messageRepository;
        private readonly IMessageMediaRepository _messageMediaRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IConversationMemberRepository _conversationMemberRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IPostRepository _postRepository;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IFileTypeDetector _fileTypeDetector;
        private readonly IStoryRepository _storyRepository;
        private readonly IMapper _mapper;
        private readonly IRealtimeService _realtimeService;
        private readonly IUnitOfWork _unitOfWork;

        public MessageService(IMessageRepository messageRepository, IMessageMediaRepository messageMediaRepository,
            IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IAccountRepository accountRepository, IPostRepository postRepository, IMapper mapper, ICloudinaryService cloudinaryService,
            IFileTypeDetector fileTypeDetector, IStoryRepository storyRepository, IRealtimeService realtimeService, IUnitOfWork unitOfWork)
        {
            _messageRepository = messageRepository;
            _messageMediaRepository = messageMediaRepository;
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _accountRepository = accountRepository;
            _postRepository = postRepository;
            _cloudinaryService = cloudinaryService;
            _fileTypeDetector = fileTypeDetector;
            _storyRepository = storyRepository;
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

            var sanitizedContent = request.Content;
            var mentionedAccountIds = new HashSet<Guid>();
            if (!string.IsNullOrEmpty(request.Content) && request.Content.Contains('@'))
            {
                var groupMembers = await _conversationMemberRepository.GetConversationMembersAsync(conversationId);
                var mentionCandidates = groupMembers
                    .Where(member => member.Account != null
                                     && member.Account.Status == AccountStatusEnum.Active
                                     && !string.IsNullOrWhiteSpace(member.Account.Username))
                    .Select(member => member.Account!)
                    .GroupBy(account => account.AccountId)
                    .Select(group => group.First())
                    .ToList();
                var mentionSanitizeResult = SanitizeGroupMentions(request.Content, mentionCandidates);
                sanitizedContent = mentionSanitizeResult.SanitizedContent;
                mentionedAccountIds = mentionSanitizeResult.MentionedAccountIds;
            }
            
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
                        Content = sanitizedContent,
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
                    await _realtimeService.NotifyNewMessageAsync(result.ConversationId, muteMap, result, mentionedAccountIds);
                    
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

        public async Task<SendMessageResponse> SendStoryReplyAsync(Guid senderId, SendStoryReplyRequest request)
        {
            // === validation phase ===
            var accounts = await _accountRepository.GetAccountsByIds(new[] { senderId, request.ReceiverId })
                ?? Enumerable.Empty<Account>();
            var sender = accounts.FirstOrDefault(a => a.AccountId == senderId)
                ?? await _accountRepository.GetAccountById(senderId);
            var receiver = accounts.FirstOrDefault(a => a.AccountId == request.ReceiverId)
                ?? await _accountRepository.GetAccountById(request.ReceiverId);

            if (receiver == null)
                throw new BadRequestException($"Receiver account with ID {request.ReceiverId} does not exist.");
            if (receiver.Status != AccountStatusEnum.Active)
                throw new BadRequestException("This user is currently unavailable.");
            if (sender == null)
                throw new BadRequestException($"Sender account with ID {senderId} does not exist.");
            if (sender.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to send messages.");

            var now = DateTime.UtcNow;

            // Verify story exists, is active, and sender is allowed to view it.
            var story = await _storyRepository.GetViewableStoryByIdAsync(senderId, request.StoryId, now);
            if (story == null)
                throw new BadRequestException("This story is no longer available.");

            if (story.AccountId != request.ReceiverId)
                throw new BadRequestException("Story receiver does not match story owner.");

            // Build snapshot JSON — stored in SystemMessageDataJson (reusing existing column)
            var storySnapshot = JsonSerializer.Serialize(new
            {
                storyId = story.StoryId,
                mediaUrl = story.MediaUrl,
                contentType = (int)story.ContentType,
                textContent = story.TextContent,
                backgroundColorKey = story.BackgroundColorKey,
                textColorKey = story.TextColorKey,
                fontTextKey = story.FontTextKey,
                fontSizeKey = story.FontSizeKey
            });

            // === database transaction phase ===
            return await _unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    // Get or create private conversation (lazy)
                    var conversationId = await _conversationRepository.GetPrivateConversationIdAsync(senderId, request.ReceiverId);
                    Guid actualConversationId;
                    if (conversationId == null)
                    {
                        var conversation = await _conversationRepository.CreatePrivateConversationAsync(senderId, request.ReceiverId);
                        await _unitOfWork.CommitAsync();
                        actualConversationId = conversation.ConversationId;
                    }
                    else
                    {
                        actualConversationId = conversationId.Value;
                    }

                    // Create StoryReply message (snapshot in SystemMessageDataJson)
                    var message = new Message
                    {
                        ConversationId = actualConversationId,
                        AccountId = senderId,
                        Content = request.Content,
                        MessageType = MessageTypeEnum.StoryReply,
                        SentAt = now,
                        IsEdited = false,
                        IsRecalled = false,
                        SystemMessageDataJson = storySnapshot
                    };
                    await _messageRepository.AddMessageAsync(message);

                    // Build response
                    var result = _mapper.Map<SendMessageResponse>(message);
                    result.TempId = request.TempId;
                    result.Sender = _mapper.Map<AccountChatInfoResponse>(sender);
                    result.StoryReplyInfo = new StoryReplyInfoModel
                    {
                        StoryId = story.StoryId,
                        IsStoryExpired = false,
                        MediaUrl = story.MediaUrl,
                        ContentType = (int)story.ContentType,
                        TextContent = story.TextContent,
                        BackgroundColorKey = story.BackgroundColorKey,
                        TextColorKey = story.TextColorKey,
                        FontTextKey = story.FontTextKey,
                        FontSizeKey = story.FontSizeKey
                    };

                    // Send realtime notification
                    var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(result.ConversationId);
                    await _realtimeService.NotifyNewMessageAsync(result.ConversationId, muteMap, result);

                    return result;
                },
                () => Task.CompletedTask
            );
        }

        public async Task<SearchPostShareTargetsResponse> SearchPostShareTargetsAsync(
            Guid senderId,
            string? keyword,
            int? limit = null)
        {
            var sender = await _accountRepository.GetAccountById(senderId);
            if (sender == null)
                throw new BadRequestException($"Sender account with ID {senderId} does not exist.");
            if (sender.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to send messages.");

            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            var safeLimit = NormalizePostShareSearchLimit(limit);

            if (normalizedKeyword.Length == 0)
            {
                var recentLimit = EmptyKeywordRecentContactsLimit;
                var (recentConversations, _) = await _conversationRepository.GetConversationsByCursorAsync(
                    senderId,
                    null,
                    null,
                    null,
                    null,
                    recentLimit);

                var recentItems = new List<PostShareTargetSearchItemResponse>();
                var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var conversation in recentConversations)
                {
                    if (conversation.IsGroup)
                    {
                        var key = $"group:{conversation.ConversationId}";
                        if (!uniqueKeys.Add(key)) continue;

                        recentItems.Add(new PostShareTargetSearchItemResponse
                        {
                            TargetType = "groupConversation",
                            ConversationId = conversation.ConversationId,
                            Name = conversation.ConversationName?.Trim() ?? conversation.DisplayName?.Trim() ?? "Group chat",
                            Subtitle = "Group conversation",
                            AvatarUrl = conversation.ConversationAvatar ?? conversation.DisplayAvatar,
                            GroupAvatars = conversation.GroupAvatars,
                            UseGroupIcon = string.IsNullOrWhiteSpace(conversation.ConversationAvatar) &&
                                           string.IsNullOrWhiteSpace(conversation.DisplayAvatar),
                            IsContacted = true,
                            LastContactedAt = conversation.LastMessageSentAt,
                            MatchScore = 0d
                        });
                        continue;
                    }

                    var otherMember = conversation.OtherMember;
                    if (otherMember == null || otherMember.AccountId == Guid.Empty)
                    {
                        continue;
                    }

                    var keyUser = $"user:{otherMember.AccountId}";
                    if (!uniqueKeys.Add(keyUser)) continue;

                    var userName = (otherMember.Username ?? string.Empty).Trim();
                    var fullName = (otherMember.FullName ?? string.Empty).Trim();
                    recentItems.Add(new PostShareTargetSearchItemResponse
                    {
                        TargetType = "user",
                        AccountId = otherMember.AccountId,
                        Name = string.IsNullOrWhiteSpace(userName) ? "Unknown user" : userName,
                        Subtitle = !string.IsNullOrWhiteSpace(fullName) &&
                                   !string.Equals(fullName, userName, StringComparison.OrdinalIgnoreCase)
                            ? fullName
                            : null,
                        AvatarUrl = otherMember.AvatarUrl,
                        UseGroupIcon = false,
                        IsContacted = true,
                        LastContactedAt = conversation.LastMessageSentAt,
                        MatchScore = 0d
                    });
                }

                return new SearchPostShareTargetsResponse
                {
                    Keyword = string.Empty,
                    Limit = recentLimit,
                    Total = recentItems.Count,
                    Items = recentItems
                };
            }

            var prefetchLimit = Math.Min(
                Math.Max(safeLimit * PostShareSearchPrefetchMultiplier, safeLimit),
                MaxPostShareSearchPrefetch);

            var userResults = await _accountRepository.SearchAccountsForPostShareAsync(
                senderId,
                normalizedKeyword,
                prefetchLimit);
            var groupResults = await _conversationRepository.SearchGroupConversationsForPostShareAsync(
                senderId,
                normalizedKeyword,
                prefetchLimit);

            var userItems = userResults.Select(user =>
            {
                var username = (user.Username ?? string.Empty).Trim();
                var fullName = (user.FullName ?? string.Empty).Trim();
                return new PostShareTargetSearchItemResponse
                {
                    TargetType = "user",
                    AccountId = user.AccountId,
                    Name = string.IsNullOrWhiteSpace(username) ? "Unknown user" : username,
                    Subtitle = !string.IsNullOrWhiteSpace(fullName) &&
                               !string.Equals(fullName, username, StringComparison.OrdinalIgnoreCase)
                        ? fullName
                        : null,
                    AvatarUrl = user.AvatarUrl,
                    UseGroupIcon = false,
                    IsContacted = user.IsContacted,
                    LastContactedAt = user.LastContactedAt,
                    MatchScore = user.MatchScore
                };
            });

            var groupItems = groupResults.Select(group => new PostShareTargetSearchItemResponse
            {
                TargetType = "groupConversation",
                ConversationId = group.ConversationId,
                Name = string.IsNullOrWhiteSpace(group.ConversationName)
                    ? "Group chat"
                    : group.ConversationName.Trim(),
                Subtitle = "Group conversation",
                AvatarUrl = group.ConversationAvatar,
                GroupAvatars = group.GroupAvatars,
                UseGroupIcon = string.IsNullOrWhiteSpace(group.ConversationAvatar),
                IsContacted = group.IsContacted,
                LastContactedAt = group.LastContactedAt,
                MatchScore = group.MatchScore
            });

            var mergedItems = userItems
                .Concat(groupItems)
                .OrderByDescending(item => item.MatchScore)
                .ThenByDescending(item => item.IsContacted)
                .ThenByDescending(item => item.LastContactedAt)
                .ThenBy(item => item.Name)
                .Take(safeLimit)
                .ToList();

            return new SearchPostShareTargetsResponse
            {
                Keyword = normalizedKeyword,
                Limit = safeLimit,
                Total = mergedItems.Count,
                Items = mergedItems
            };
        }

        public async Task<SendPostShareResponse> SendPostShareAsync(Guid senderId, SendPostShareRequest request)
        {
            if (request == null)
                throw new BadRequestException("Request is required.");

            if (request.PostId == Guid.Empty)
                throw new BadRequestException("Post ID is required.");

            var sender = await _accountRepository.GetAccountById(senderId);
            if (sender == null)
                throw new BadRequestException($"Sender account with ID {senderId} does not exist.");
            if (sender.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to send messages.");

            var conversationIds = (request.ConversationIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
            var receiverIds = (request.ReceiverIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (!conversationIds.Any() && !receiverIds.Any())
                throw new BadRequestException("At least one recipient is required.");

            var requestedRecipientCount = conversationIds.Count + receiverIds.Count;
            if (requestedRecipientCount > MaxPostShareRecipientsPerRequest)
                throw new BadRequestException($"You can share to up to {MaxPostShareRecipientsPerRequest} recipients at once.");

            var post = await _postRepository.GetPostDetailByPostId(request.PostId, senderId);
            if (post == null)
                throw new BadRequestException("This post is no longer available.");

            var normalizedContent = NormalizeOptionalMessageContent(request.Content);
            var shareInfo = BuildPostShareInfo(post);
            var snapshotJson = JsonSerializer.Serialize(new
            {
                postId = shareInfo.PostId,
                postCode = shareInfo.PostCode,
                ownerId = shareInfo.OwnerId,
                ownerUsername = shareInfo.OwnerUsername,
                ownerDisplayName = shareInfo.OwnerDisplayName,
                thumbnailUrl = shareInfo.ThumbnailUrl,
                thumbnailMediaType = shareInfo.ThumbnailMediaType,
                contentSnippet = shareInfo.ContentSnippet
            });

            var senderDto = _mapper.Map<AccountChatInfoResponse>(sender);
            var processedConversationIds = new HashSet<Guid>();
            var results = new List<PostShareSendResult>();
            var receiverLookup = new Dictionary<Guid, Account>();
            if (receiverIds.Any())
            {
                var receiverAccounts = await _accountRepository.GetAccountsByIds(receiverIds) ?? new List<Account>();
                receiverLookup = receiverAccounts.ToDictionary(account => account.AccountId, account => account);
            }

            foreach (var conversationId in conversationIds)
            {
                if (!processedConversationIds.Add(conversationId))
                {
                    results.Add(BuildDuplicatePostShareResult(conversationId, null, results));
                    continue;
                }

                var result = await SendPostShareToConversationAsync(
                    senderId,
                    senderDto,
                    conversationId,
                    null,
                    normalizedContent,
                    request.TempId,
                    snapshotJson,
                    shareInfo);
                results.Add(result);
            }

            foreach (var receiverId in receiverIds)
            {
                if (receiverId == senderId)
                {
                    results.Add(new PostShareSendResult
                    {
                        ReceiverId = receiverId,
                        IsSuccess = false,
                        ErrorMessage = "You cannot send to yourself."
                    });
                    continue;
                }

                try
                {
                    if (!receiverLookup.TryGetValue(receiverId, out var receiver) || receiver == null)
                    {
                        results.Add(new PostShareSendResult
                        {
                            ReceiverId = receiverId,
                            IsSuccess = false,
                            ErrorMessage = "Receiver account does not exist."
                        });
                        continue;
                    }

                    if (receiver.Status != AccountStatusEnum.Active)
                    {
                        results.Add(new PostShareSendResult
                        {
                            ReceiverId = receiverId,
                            IsSuccess = false,
                            ErrorMessage = "This user is currently unavailable."
                        });
                        continue;
                    }

                    var conversationId =
                        await _conversationRepository.GetPrivateConversationIdAsync(senderId, receiverId);
                    if (conversationId == null)
                    {
                        var conversation = await _conversationRepository.CreatePrivateConversationAsync(senderId, receiverId);
                        await _unitOfWork.CommitAsync();
                        conversationId = conversation.ConversationId;
                    }

                    if (!processedConversationIds.Add(conversationId.Value))
                    {
                        results.Add(BuildDuplicatePostShareResult(conversationId.Value, receiverId, results));
                        continue;
                    }

                    var result = await SendPostShareToConversationAsync(
                        senderId,
                        senderDto,
                        conversationId.Value,
                        receiverId,
                        normalizedContent,
                        request.TempId,
                        snapshotJson,
                        shareInfo,
                        skipMembershipValidation: true);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    results.Add(new PostShareSendResult
                    {
                        ReceiverId = receiverId,
                        IsSuccess = false,
                        ErrorMessage = ResolvePostShareFailureMessage(ex)
                    });
                }
            }

            return new SendPostShareResponse
            {
                TotalRequested = results.Count,
                TotalSucceeded = results.Count(r => r.IsSuccess),
                TotalFailed = results.Count(r => !r.IsSuccess),
                Results = results
            };
        }

        public async Task<SendPostShareResponse> ForwardMessageAsync(Guid senderId, ForwardMessageRequest request)
        {
            if (request == null)
                throw new BadRequestException("Request is required.");

            if (request.SourceMessageId == Guid.Empty)
                throw new BadRequestException("Source message ID is required.");

            var sender = await _accountRepository.GetAccountById(senderId);
            if (sender == null)
                throw new BadRequestException($"Sender account with ID {senderId} does not exist.");
            if (sender.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to send messages.");

            var conversationIds = (request.ConversationIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
            var receiverIds = (request.ReceiverIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (!conversationIds.Any() && !receiverIds.Any())
                throw new BadRequestException("At least one recipient is required.");

            var requestedRecipientCount = conversationIds.Count + receiverIds.Count;
            if (requestedRecipientCount > MaxForwardRecipientsPerRequest)
                throw new BadRequestException($"You can forward to up to {MaxForwardRecipientsPerRequest} recipients at once.");

            var sourceMessage = await _messageRepository.GetVisibleMessageForAccountAsync(request.SourceMessageId, senderId);
            if (sourceMessage == null)
                throw new BadRequestException("This message is no longer available.");

            if (sourceMessage.IsRecalled)
                throw new BadRequestException("This message can no longer be forwarded.");

            if (sourceMessage.MessageType == MessageTypeEnum.System)
                throw new BadRequestException("System messages cannot be forwarded.");

            if (sourceMessage.MessageType == MessageTypeEnum.StoryReply)
                throw new BadRequestException("Story reply messages cannot be forwarded.");

            if (sourceMessage.MessageType != MessageTypeEnum.Text &&
                sourceMessage.MessageType != MessageTypeEnum.Media &&
                sourceMessage.MessageType != MessageTypeEnum.PostShare)
            {
                throw new BadRequestException("This message type cannot be forwarded.");
            }

            if (sourceMessage.MessageType == MessageTypeEnum.Text &&
                string.IsNullOrWhiteSpace(sourceMessage.Content))
            {
                throw new BadRequestException("This message can no longer be forwarded.");
            }

            if (sourceMessage.MessageType == MessageTypeEnum.PostShare &&
                string.IsNullOrWhiteSpace(sourceMessage.SystemMessageDataJson))
            {
                throw new BadRequestException("This shared post message can no longer be forwarded.");
            }

            var sourceMedias = sourceMessage.MessageType == MessageTypeEnum.Media
                ? await _messageMediaRepository.GetByMessageIdAsync(sourceMessage.MessageId)
                : new List<MessageMedia>();

            if (sourceMessage.MessageType == MessageTypeEnum.Media && !sourceMedias.Any())
                throw new BadRequestException("This media message can no longer be forwarded.");

            PostShareInfoModel? sourcePostShareInfo = null;
            if (sourceMessage.MessageType == MessageTypeEnum.PostShare)
            {
                sourcePostShareInfo = await BuildForwardPostShareInfoAsync(senderId, sourceMessage.SystemMessageDataJson);
            }

            var senderDto = _mapper.Map<AccountChatInfoResponse>(sender);
            var processedConversationIds = new HashSet<Guid>();
            var results = new List<PostShareSendResult>();
            var receiverLookup = new Dictionary<Guid, Account>();

            if (receiverIds.Any())
            {
                var receiverAccounts = await _accountRepository.GetAccountsByIds(receiverIds) ?? new List<Account>();
                receiverLookup = receiverAccounts.ToDictionary(account => account.AccountId, account => account);
            }

            foreach (var conversationId in conversationIds)
            {
                if (!processedConversationIds.Add(conversationId))
                {
                    results.Add(BuildDuplicateForwardResult(conversationId, null, results));
                    continue;
                }

                var result = await ForwardMessageToConversationAsync(
                    senderId,
                    senderDto,
                    conversationId,
                    null,
                    request.TempId,
                    sourceMessage,
                    sourceMedias,
                    sourcePostShareInfo);
                results.Add(result);
            }

            foreach (var receiverId in receiverIds)
            {
                if (receiverId == senderId)
                {
                    results.Add(new PostShareSendResult
                    {
                        ReceiverId = receiverId,
                        IsSuccess = false,
                        ErrorMessage = "You cannot send to yourself."
                    });
                    continue;
                }

                try
                {
                    if (!receiverLookup.TryGetValue(receiverId, out var receiver) || receiver == null)
                    {
                        results.Add(new PostShareSendResult
                        {
                            ReceiverId = receiverId,
                            IsSuccess = false,
                            ErrorMessage = "Receiver account does not exist."
                        });
                        continue;
                    }

                    if (receiver.Status != AccountStatusEnum.Active)
                    {
                        results.Add(new PostShareSendResult
                        {
                            ReceiverId = receiverId,
                            IsSuccess = false,
                            ErrorMessage = "This user is currently unavailable."
                        });
                        continue;
                    }

                    var conversationId = await _conversationRepository.GetPrivateConversationIdAsync(senderId, receiverId);
                    if (conversationId == null)
                    {
                        var conversation = await _conversationRepository.CreatePrivateConversationAsync(senderId, receiverId);
                        await _unitOfWork.CommitAsync();
                        conversationId = conversation.ConversationId;
                    }

                    if (!processedConversationIds.Add(conversationId.Value))
                    {
                        results.Add(BuildDuplicateForwardResult(conversationId.Value, receiverId, results));
                        continue;
                    }

                    var result = await ForwardMessageToConversationAsync(
                        senderId,
                        senderDto,
                        conversationId.Value,
                        receiverId,
                        request.TempId,
                        sourceMessage,
                        sourceMedias,
                        sourcePostShareInfo,
                        skipMembershipValidation: true);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    results.Add(new PostShareSendResult
                    {
                        ReceiverId = receiverId,
                        IsSuccess = false,
                        ErrorMessage = ResolveForwardFailureMessage(ex)
                    });
                }
            }

            return new SendPostShareResponse
            {
                TotalRequested = results.Count,
                TotalSucceeded = results.Count(r => r.IsSuccess),
                TotalFailed = results.Count(r => !r.IsSuccess),
                Results = results
            };
        }

        private async Task<PostShareSendResult> SendPostShareToConversationAsync(
            Guid senderId,
            AccountChatInfoResponse sender,
            Guid conversationId,
            Guid? receiverId,
            string? content,
            string? tempId,
            string snapshotJson,
            PostShareInfoModel shareInfo,
            bool skipMembershipValidation = false)
        {
            try
            {
                if (!skipMembershipValidation)
                {
                    var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
                    if (conversation == null)
                    {
                        return new PostShareSendResult
                        {
                            ConversationId = conversationId,
                            ReceiverId = receiverId,
                            IsSuccess = false,
                            ErrorMessage = "Conversation not found."
                        };
                    }

                    if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, senderId))
                    {
                        return new PostShareSendResult
                        {
                            ConversationId = conversationId,
                            ReceiverId = receiverId,
                            IsSuccess = false,
                            ErrorMessage = "You are not a member of this conversation."
                        };
                    }
                }

                var message = await _unitOfWork.ExecuteInTransactionAsync(
                    async () =>
                    {
                        var entity = new Message
                        {
                            ConversationId = conversationId,
                            AccountId = senderId,
                            Content = content,
                            MessageType = MessageTypeEnum.PostShare,
                            SentAt = DateTime.UtcNow,
                            IsEdited = false,
                            IsRecalled = false,
                            SystemMessageDataJson = snapshotJson
                        };
                        await _messageRepository.AddMessageAsync(entity);

                        var result = _mapper.Map<SendMessageResponse>(entity);
                        result.TempId = tempId;
                        result.Sender = sender;
                        result.PostShareInfo = ClonePostShareInfo(shareInfo);

                        var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
                        await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, result);

                        return result;
                    },
                    () => Task.CompletedTask);

                return new PostShareSendResult
                {
                    ConversationId = conversationId,
                    ReceiverId = receiverId,
                    IsSuccess = true,
                    Message = message
                };
            }
            catch (Exception ex)
            {
                return new PostShareSendResult
                {
                    ConversationId = conversationId,
                    ReceiverId = receiverId,
                    IsSuccess = false,
                    ErrorMessage = ResolvePostShareFailureMessage(ex)
                };
            }
        }

        private async Task<PostShareSendResult> ForwardMessageToConversationAsync(
            Guid senderId,
            AccountChatInfoResponse sender,
            Guid conversationId,
            Guid? receiverId,
            string? tempId,
            Message sourceMessage,
            IReadOnlyList<MessageMedia> sourceMedias,
            PostShareInfoModel? sourcePostShareInfo,
            bool skipMembershipValidation = false)
        {
            try
            {
                if (!skipMembershipValidation)
                {
                    var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
                    if (conversation == null)
                    {
                        return new PostShareSendResult
                        {
                            ConversationId = conversationId,
                            ReceiverId = receiverId,
                            IsSuccess = false,
                            ErrorMessage = "Conversation not found."
                        };
                    }

                    if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, senderId))
                    {
                        return new PostShareSendResult
                        {
                            ConversationId = conversationId,
                            ReceiverId = receiverId,
                            IsSuccess = false,
                            ErrorMessage = "You are not a member of this conversation."
                        };
                    }
                }

                var message = await _unitOfWork.ExecuteInTransactionAsync(
                    async () =>
                    {
                        var now = DateTime.UtcNow;
                        var forwardedContent = ConvertMentionsToPlainText(sourceMessage.Content);
                        var entity = new Message
                        {
                            ConversationId = conversationId,
                            AccountId = senderId,
                            Content = forwardedContent,
                            MessageType = sourceMessage.MessageType,
                            SentAt = now,
                            IsEdited = false,
                            IsRecalled = false,
                            SystemMessageDataJson = sourceMessage.MessageType == MessageTypeEnum.PostShare
                                ? sourceMessage.SystemMessageDataJson
                                : null
                        };
                        await _messageRepository.AddMessageAsync(entity);

                        List<MessageMedia>? clonedMedias = null;
                        if (sourceMessage.MessageType == MessageTypeEnum.Media && sourceMedias.Count > 0)
                        {
                            clonedMedias = sourceMedias
                                .Select(media => new MessageMedia
                                {
                                    MessageId = entity.MessageId,
                                    MediaUrl = media.MediaUrl,
                                    ThumbnailUrl = media.ThumbnailUrl,
                                    MediaType = media.MediaType,
                                    FileName = media.FileName,
                                    FileSize = media.FileSize,
                                    CreatedAt = now
                                })
                                .ToList();

                            await _messageMediaRepository.AddMessageMediasAsync(clonedMedias);
                        }

                        var result = _mapper.Map<SendMessageResponse>(entity);
                        result.TempId = tempId;
                        result.Sender = sender;

                        if (clonedMedias != null && clonedMedias.Count > 0)
                        {
                            result.Medias = _mapper.Map<List<MessageMediaResponse>>(clonedMedias);
                        }

                        if (entity.MessageType == MessageTypeEnum.PostShare)
                        {
                            result.PostShareInfo = sourcePostShareInfo != null
                                ? ClonePostShareInfo(sourcePostShareInfo)
                                : new PostShareInfoModel { IsPostUnavailable = true };
                        }

                        var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
                        await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, result);

                        return result;
                    },
                    () => Task.CompletedTask);

                return new PostShareSendResult
                {
                    ConversationId = conversationId,
                    ReceiverId = receiverId,
                    IsSuccess = true,
                    Message = message
                };
            }
            catch (Exception ex)
            {
                return new PostShareSendResult
                {
                    ConversationId = conversationId,
                    ReceiverId = receiverId,
                    IsSuccess = false,
                    ErrorMessage = ResolveForwardFailureMessage(ex)
                };
            }
        }

        private static int NormalizePostShareSearchLimit(int? limit)
        {
            if (!limit.HasValue || limit.Value <= 0)
            {
                return DefaultPostShareSearchLimit;
            }

            return Math.Min(limit.Value, MaxPostShareSearchLimit);
        }

        private static string? NormalizeOptionalMessageContent(string? content)
        {
            var normalized = (content ?? string.Empty).Trim();
            return normalized.Length == 0 ? null : normalized;
        }

        private static string? ConvertMentionsToPlainText(string? content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return content;
            }

            var mentionTokens = MentionParser.ExtractTokens(content);
            if (mentionTokens.Count == 0)
            {
                return content;
            }

            var builder = new StringBuilder(content.Length);
            var currentIndex = 0;

            foreach (var token in mentionTokens)
            {
                if (token.StartIndex > currentIndex)
                {
                    builder.Append(content.AsSpan(currentIndex, token.StartIndex - currentIndex));
                }

                var plainMention = MentionParser.BuildPlainMentionText(token.Username);
                builder.Append(string.IsNullOrWhiteSpace(plainMention) ? token.RawText : plainMention);
                currentIndex = token.StartIndex + token.Length;
            }

            if (currentIndex < content.Length)
            {
                builder.Append(content.AsSpan(currentIndex));
            }

            return builder.ToString();
        }

        private static (string? SanitizedContent, HashSet<Guid> MentionedAccountIds) SanitizeGroupMentions(
            string? content,
            IReadOnlyCollection<Account> mentionCandidates)
        {
            var safeContent = content ?? string.Empty;
            var mentionTokens = MentionParser.ExtractTokens(safeContent);
            var mentionedAccountIds = new HashSet<Guid>();
            if (mentionTokens.Count == 0)
            {
                return (content, mentionedAccountIds);
            }

            var activeCandidates = (mentionCandidates ?? Array.Empty<Account>())
                .Where(account => account.Status == AccountStatusEnum.Active
                                  && !string.IsNullOrWhiteSpace(account.Username))
                .ToList();

            var accountById = activeCandidates
                .GroupBy(account => account.AccountId)
                .ToDictionary(group => group.Key, group => group.First());
            var accountByUsername = activeCandidates
                .GroupBy(account => (account.Username ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var builder = new StringBuilder();
            var currentIndex = 0;
            foreach (var token in mentionTokens)
            {
                if (token.StartIndex > currentIndex)
                {
                    builder.Append(safeContent.AsSpan(currentIndex, token.StartIndex - currentIndex));
                }

                var resolvedAccount = ResolveMentionTargetAccount(token, accountById, accountByUsername);
                if (resolvedAccount != null)
                {
                    builder.Append(MentionParser.BuildCanonicalMentionText(resolvedAccount.Username, resolvedAccount.AccountId));
                    mentionedAccountIds.Add(resolvedAccount.AccountId);
                }
                else
                {
                    builder.Append(MentionParser.BuildPlainMentionText(token.Username));
                }

                currentIndex = token.StartIndex + token.Length;
            }

            if (currentIndex < safeContent.Length)
            {
                builder.Append(safeContent.AsSpan(currentIndex));
            }

            return (builder.ToString(), mentionedAccountIds);
        }

        private static Account? ResolveMentionTargetAccount(
            MentionParser.MentionToken token,
            IReadOnlyDictionary<Guid, Account> accountById,
            IReadOnlyDictionary<string, Account> accountByUsername)
        {
            if (token.IsCanonical
                && token.AccountId.HasValue
                && accountById.TryGetValue(token.AccountId.Value, out var accountByTokenId))
            {
                return accountByTokenId;
            }

            var normalizedUsername = MentionParser.NormalizeUsername(token.Username);
            if (string.IsNullOrWhiteSpace(normalizedUsername))
            {
                return null;
            }

            return accountByUsername.TryGetValue(normalizedUsername, out var accountByTokenUsername)
                ? accountByTokenUsername
                : null;
        }

        private static string ResolvePostShareFailureMessage(Exception ex)
        {
            if (ex is BadRequestException ||
                ex is ForbiddenException ||
                ex is NotFoundException)
            {
                return ex.Message;
            }

            return "Failed to share post.";
        }

        private static string ResolveForwardFailureMessage(Exception ex)
        {
            if (ex is BadRequestException ||
                ex is ForbiddenException ||
                ex is NotFoundException)
            {
                return ex.Message;
            }

            return "Failed to forward message.";
        }

        private static PostShareSendResult BuildDuplicatePostShareResult(
            Guid conversationId,
            Guid? receiverId,
            IReadOnlyList<PostShareSendResult> currentResults)
        {
            var existing = currentResults.LastOrDefault(item => item.ConversationId == conversationId);
            if (existing == null)
            {
                return new PostShareSendResult
                {
                    ConversationId = conversationId,
                    ReceiverId = receiverId,
                    IsSuccess = false,
                    ErrorMessage = "Failed to share post."
                };
            }

            return new PostShareSendResult
            {
                ConversationId = conversationId,
                ReceiverId = receiverId,
                IsSuccess = existing.IsSuccess,
                ErrorMessage = existing.IsSuccess ? null : existing.ErrorMessage,
                Message = existing.IsSuccess ? existing.Message : null
            };
        }

        private static PostShareSendResult BuildDuplicateForwardResult(
            Guid conversationId,
            Guid? receiverId,
            IReadOnlyList<PostShareSendResult> currentResults)
        {
            var existing = currentResults.LastOrDefault(item => item.ConversationId == conversationId);
            if (existing == null)
            {
                return new PostShareSendResult
                {
                    ConversationId = conversationId,
                    ReceiverId = receiverId,
                    IsSuccess = false,
                    ErrorMessage = "Failed to forward message."
                };
            }

            return new PostShareSendResult
            {
                ConversationId = conversationId,
                ReceiverId = receiverId,
                IsSuccess = existing.IsSuccess,
                ErrorMessage = existing.IsSuccess ? null : existing.ErrorMessage,
                Message = existing.IsSuccess ? existing.Message : null
            };
        }

        private static string? BuildPostContentSnippet(string? content)
        {
            var normalized = (content ?? string.Empty).Trim();
            if (normalized.Length == 0) return null;
            const int maxLength = 120;
            if (normalized.Length <= maxLength) return normalized;
            return normalized.Substring(0, maxLength - 3) + "...";
        }

        private static PostShareInfoModel BuildPostShareInfo(PostDetailModel post)
        {
            var firstMedia = post.Medias?.FirstOrDefault();
            return new PostShareInfoModel
            {
                PostId = post.PostId,
                PostCode = post.PostCode ?? string.Empty,
                IsPostUnavailable = false,
                OwnerId = post.Owner?.AccountId ?? Guid.Empty,
                OwnerUsername = post.Owner?.Username,
                OwnerDisplayName = post.Owner?.FullName,
                ThumbnailUrl = firstMedia?.MediaUrl,
                ThumbnailMediaType = firstMedia != null ? (int?)firstMedia.MediaType : null,
                ContentSnippet = BuildPostContentSnippet(post.Content)
            };
        }

        private static PostShareInfoModel ClonePostShareInfo(PostShareInfoModel source)
        {
            return new PostShareInfoModel
            {
                PostId = source.PostId,
                PostCode = source.PostCode,
                IsPostUnavailable = source.IsPostUnavailable,
                OwnerId = source.OwnerId,
                OwnerUsername = source.OwnerUsername,
                OwnerDisplayName = source.OwnerDisplayName,
                ThumbnailUrl = source.ThumbnailUrl,
                ThumbnailMediaType = source.ThumbnailMediaType,
                ContentSnippet = source.ContentSnippet
            };
        }

        private async Task<PostShareInfoModel> BuildForwardPostShareInfoAsync(Guid currentId, string? snapshotJson)
        {
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return new PostShareInfoModel { IsPostUnavailable = true };
            }

            try
            {
                using var doc = JsonDocument.Parse(snapshotJson);
                var root = doc.RootElement;

                Guid.TryParse(root.TryGetProperty("postId", out var postIdProp) ? postIdProp.GetString() : null, out var postId);
                Guid.TryParse(root.TryGetProperty("ownerId", out var ownerIdProp) ? ownerIdProp.GetString() : null, out var ownerId);

                int? thumbnailMediaType = null;
                if (root.TryGetProperty("thumbnailMediaType", out var thumbTypeProp))
                {
                    if (thumbTypeProp.ValueKind == JsonValueKind.Number && thumbTypeProp.TryGetInt32(out var numericType))
                    {
                        thumbnailMediaType = numericType;
                    }
                    else if (thumbTypeProp.ValueKind == JsonValueKind.String && int.TryParse(thumbTypeProp.GetString(), out var parsedType))
                    {
                        thumbnailMediaType = parsedType;
                    }
                }

                var info = new PostShareInfoModel
                {
                    PostId = postId,
                    PostCode = root.TryGetProperty("postCode", out var postCodeProp) ? postCodeProp.GetString() ?? string.Empty : string.Empty,
                    IsPostUnavailable = false,
                    OwnerId = ownerId,
                    OwnerUsername = root.TryGetProperty("ownerUsername", out var ownerUsernameProp) ? ownerUsernameProp.GetString() : null,
                    OwnerDisplayName = root.TryGetProperty("ownerDisplayName", out var ownerDisplayNameProp) ? ownerDisplayNameProp.GetString() : null,
                    ThumbnailUrl = root.TryGetProperty("thumbnailUrl", out var thumbUrlProp) ? thumbUrlProp.GetString() : null,
                    ThumbnailMediaType = thumbnailMediaType,
                    ContentSnippet = root.TryGetProperty("contentSnippet", out var snippetProp) ? snippetProp.GetString() : null
                };

                var isUnavailable = info.PostId == Guid.Empty;
                if (!isUnavailable)
                {
                    var post = await _postRepository.GetPostDetailByPostId(info.PostId, currentId);
                    isUnavailable = post == null;
                }

                info.IsPostUnavailable = isUnavailable;
                if (isUnavailable)
                {
                    info.ThumbnailUrl = null;
                    info.ThumbnailMediaType = null;
                    info.ContentSnippet = null;
                }

                return info;
            }
            catch
            {
                return new PostShareInfoModel { IsPostUnavailable = true };
            }
        }

    }
}
