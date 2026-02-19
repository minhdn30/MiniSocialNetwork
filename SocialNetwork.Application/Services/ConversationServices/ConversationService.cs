using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using SocialNetwork.Application.DTOs.ConversationMemberDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.Services.RealtimeServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.Messages;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.ConversationServices
{
    public class ConversationService : IConversationService
    {
        private const int MinGroupConversationMembers = 3;
        private const int MaxGroupConversationMembers = 50;
        private const int DefaultGroupMembersPageSize = 20;
        private const int MaxGroupMembersPageSize = 100;

        private readonly IConversationRepository _conversationRepository;
        private readonly IConversationMemberRepository _conversationMemberRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IFollowRepository _followRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IMapper _mapper;
        private readonly IRealtimeService _realtimeService;
        public ConversationService(IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IMessageRepository messageRepository, IAccountRepository accountRepository, IFollowRepository followRepository,
            IUnitOfWork unitOfWork, ICloudinaryService cloudinaryService, IMapper mapper, IRealtimeService realtimeService)
        {
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _messageRepository = messageRepository;
            _accountRepository = accountRepository;
            _followRepository = followRepository;
            _unitOfWork = unitOfWork;
            _cloudinaryService = cloudinaryService;
            _mapper = mapper;
            _realtimeService = realtimeService;
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

        public async Task<ConversationResponse> CreateGroupConversationAsync(Guid currentId, CreateGroupConversationRequest request)
        {
            if (request == null)
                throw new BadRequestException("Request is required.");

            var normalizedGroupName = request.GroupName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedGroupName))
                throw new BadRequestException("Group name is required.");

            var uniqueOtherMemberIds = (request.MemberIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty && id != currentId)
                .Distinct()
                .ToList();

            var totalMembers = uniqueOtherMemberIds.Count + 1;
            if (totalMembers < MinGroupConversationMembers)
                throw new BadRequestException("A group must have at least 3 members (you and 2 others).");

            if (totalMembers > MaxGroupConversationMembers)
                throw new BadRequestException($"A group can contain at most {MaxGroupConversationMembers} members.");

            var allMemberIds = uniqueOtherMemberIds
                .Append(currentId)
                .Distinct()
                .ToList();

            var allAccounts = await _accountRepository.GetAccountsByIds(allMemberIds);
            var creator = allAccounts.FirstOrDefault(a => a.AccountId == currentId);
            if (creator == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");
            if (creator.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to create a group.");

            var targetAccounts = allAccounts
                .Where(a => a.AccountId != currentId)
                .ToList();

            if (targetAccounts.Count != uniqueOtherMemberIds.Count)
                throw new BadRequestException("One or more selected members do not exist.");

            var inactiveAccounts = targetAccounts.Where(a => a.Status != AccountStatusEnum.Active).ToList();
            if (inactiveAccounts.Count > 0)
            {
                var inactiveMentions = string.Join(", ", inactiveAccounts
                    .Select(a => ToMention(a.Username))
                    .Distinct()
                    .Take(5));
                var suffix = inactiveAccounts.Count > 5 ? " ..." : string.Empty;
                throw new BadRequestException($"These members are not active: {inactiveMentions}{suffix}");
            }

            var connectedAccountIds = await _followRepository.GetConnectedAccountIdsAsync(currentId, uniqueOtherMemberIds);

            var permissionDeniedTargets = targetAccounts
                .Where(target =>
                {
                    var permission = target.Settings?.GroupChatInvitePermission ?? GroupChatInvitePermissionEnum.Anyone;
                    if (permission == GroupChatInvitePermissionEnum.Anyone)
                    {
                        return false;
                    }

                    if (permission == GroupChatInvitePermissionEnum.NoOne)
                    {
                        return true;
                    }

                    return !connectedAccountIds.Contains(target.AccountId);
                })
                .ToList();

            if (permissionDeniedTargets.Count > 0)
            {
                var deniedMentions = string.Join(", ", permissionDeniedTargets
                    .Select(a => ToMention(a.Username))
                    .Distinct()
                    .Take(5));
                var suffix = permissionDeniedTargets.Count > 5 ? " ..." : string.Empty;
                throw new BadRequestException($"You are not allowed to add these members due to invite privacy: {deniedMentions}{suffix}");
            }

            string? uploadedGroupAvatarUrl = null;
            if (request.GroupAvatar != null)
            {
                if (request.GroupAvatar.Length <= 0)
                    throw new BadRequestException("Group avatar file is empty.");

                uploadedGroupAvatarUrl = await _cloudinaryService.UploadImageAsync(request.GroupAvatar);
                if (string.IsNullOrWhiteSpace(uploadedGroupAvatarUrl))
                    throw new InternalServerException("Group avatar upload failed.");
            }

            var nowUtc = DateTime.UtcNow;
            var conversation = new Conversation
            {
                ConversationId = Guid.NewGuid(),
                ConversationName = normalizedGroupName,
                ConversationAvatar = uploadedGroupAvatarUrl,
                IsGroup = true,
                CreatedAt = nowUtc,
                CreatedBy = currentId,
                IsDeleted = false
            };

            var memberEntities = new List<ConversationMember>
            {
                new ConversationMember
                {
                    ConversationId = conversation.ConversationId,
                    AccountId = currentId,
                    IsAdmin = true,
                    JoinedAt = nowUtc
                }
            };

            memberEntities.AddRange(targetAccounts.Select(target => new ConversationMember
            {
                ConversationId = conversation.ConversationId,
                AccountId = target.AccountId,
                IsAdmin = false,
                JoinedAt = nowUtc
            }));

            var systemMessage = new Message
            {
                ConversationId = conversation.ConversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = $"{ToMention(creator.Username)} created this group.",
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.GroupCreated,
                    actorAccountId = currentId,
                    actorUsername = creator.Username,
                    conversationName = normalizedGroupName,
                    memberCount = totalMembers
                }),
                SentAt = nowUtc,
                IsEdited = false,
                IsRecalled = false
            };

            await _unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    await _conversationRepository.AddConversationAsync(conversation);
                    await _conversationMemberRepository.AddConversationMembers(memberEntities);
                    await _messageRepository.AddMessageAsync(systemMessage);
                    return true;
                },
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(uploadedGroupAvatarUrl))
                        return;

                    var publicId = _cloudinaryService.GetPublicIdFromUrl(uploadedGroupAvatarUrl);
                    if (!string.IsNullOrWhiteSpace(publicId))
                    {
                        await _cloudinaryService.DeleteMediaAsync(publicId, MediaTypeEnum.Image);
                    }
                });

            var accountMap = allAccounts.ToDictionary(a => a.AccountId, a => a);

            return new ConversationResponse
            {
                ConversationId = conversation.ConversationId,
                ConversationName = conversation.ConversationName,
                ConversationAvatar = conversation.ConversationAvatar,
                Theme = conversation.Theme,
                IsGroup = conversation.IsGroup,
                CreatedAt = conversation.CreatedAt,
                CreatedBy = conversation.CreatedBy,
                IsDeleted = conversation.IsDeleted,
                Members = memberEntities.Select(member =>
                {
                    accountMap.TryGetValue(member.AccountId, out var account);
                    return new ConversationMemberResponse
                    {
                        ConversationId = member.ConversationId,
                        Nickname = member.Nickname,
                        JoinedAt = member.JoinedAt,
                        IsAdmin = member.IsAdmin,
                        HasLeft = member.HasLeft,
                        LastSeenMessageId = member.LastSeenMessageId,
                        IsMuted = member.IsMuted,
                        IsDeleted = member.IsDeleted,
                        ClearedAt = member.ClearedAt,
                        Account = new AccountBasicInfoResponse
                        {
                            AccountId = member.AccountId,
                            Username = account?.Username ?? string.Empty,
                            FullName = account?.FullName ?? string.Empty,
                            AvatarUrl = account?.AvatarUrl,
                            Status = account?.Status ?? AccountStatusEnum.Active
                        }
                    };
                }).ToList()
            };
        }

        public async Task UpdateGroupConversationInfoAsync(Guid conversationId, Guid currentId, UpdateGroupConversationRequest request)
        {
            if (request == null)
                throw new BadRequestException("Request is required.");

            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
                throw new ForbiddenException("You are not a member of this conversation.");

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Only group conversations can be updated.");

            var actor = await _accountRepository.GetAccountById(currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var hasConversationNameInput = request.ConversationName != null;
            var normalizedConversationName = request.ConversationName?.Trim();
            if (hasConversationNameInput && string.IsNullOrWhiteSpace(normalizedConversationName))
                throw new BadRequestException("Conversation name cannot be empty.");

            var hasConversationNameChanged =
                hasConversationNameInput &&
                !string.Equals(conversation.ConversationName, normalizedConversationName, StringComparison.Ordinal);

            var hasConversationAvatarInput = request.ConversationAvatar != null;
            var hasRemoveConversationAvatarInput = request.RemoveAvatar;
            if (hasConversationAvatarInput && hasRemoveConversationAvatarInput)
                throw new BadRequestException("You cannot upload and remove avatar in the same request.");

            if (hasConversationAvatarInput && request.ConversationAvatar!.Length <= 0)
                throw new BadRequestException("Conversation avatar file is empty.");

            var nextConversationAvatar = conversation.ConversationAvatar;
            string? uploadedConversationAvatarUrl = null;
            if (hasConversationAvatarInput)
            {
                uploadedConversationAvatarUrl = await _cloudinaryService.UploadImageAsync(request.ConversationAvatar!);
                if (string.IsNullOrWhiteSpace(uploadedConversationAvatarUrl))
                    throw new InternalServerException("Conversation avatar upload failed.");

                nextConversationAvatar = uploadedConversationAvatarUrl;
            }
            else if (hasRemoveConversationAvatarInput)
            {
                nextConversationAvatar = null;
            }

            var hasConversationAvatarChanged =
                !string.Equals(conversation.ConversationAvatar, nextConversationAvatar, StringComparison.Ordinal);

            if (!hasConversationNameChanged && !hasConversationAvatarChanged)
                return;

            var oldConversationAvatar = conversation.ConversationAvatar;

            if (hasConversationNameChanged)
                conversation.ConversationName = normalizedConversationName;

            if (hasConversationAvatarChanged)
                conversation.ConversationAvatar = nextConversationAvatar;

            var isAvatarRemoved = hasConversationAvatarChanged && string.IsNullOrWhiteSpace(nextConversationAvatar);
            var systemAction = hasConversationNameChanged && hasConversationAvatarChanged
                ? SystemMessageActionEnum.GroupInfoUpdated
                : hasConversationNameChanged
                    ? SystemMessageActionEnum.GroupRenamed
                    : SystemMessageActionEnum.GroupAvatarUpdated;

            var systemMessage = new Message
            {
                ConversationId = conversation.ConversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildGroupInfoSystemMessageFallback(
                    actor.Username,
                    conversation.ConversationName,
                    hasConversationNameChanged,
                    hasConversationAvatarChanged,
                    isAvatarRemoved),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)systemAction,
                    actorAccountId = currentId,
                    actorUsername = actor.Username,
                    conversationName = conversation.ConversationName,
                    conversationAvatar = conversation.ConversationAvatar,
                    hasConversationNameChanged,
                    hasConversationAvatarChanged,
                    avatarRemoved = isAvatarRemoved
                }),
                SentAt = DateTime.UtcNow,
                IsEdited = false,
                IsRecalled = false
            };

            await _unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    await _conversationRepository.UpdateConversationAsync(conversation);
                    await _messageRepository.AddMessageAsync(systemMessage);
                    return true;
                },
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(uploadedConversationAvatarUrl))
                        return;

                    var newAvatarPublicId = _cloudinaryService.GetPublicIdFromUrl(uploadedConversationAvatarUrl);
                    if (!string.IsNullOrWhiteSpace(newAvatarPublicId))
                    {
                        await _cloudinaryService.DeleteMediaAsync(newAvatarPublicId, MediaTypeEnum.Image);
                    }
                });

            if (hasConversationAvatarChanged &&
                !string.IsNullOrWhiteSpace(oldConversationAvatar) &&
                !string.Equals(oldConversationAvatar, conversation.ConversationAvatar, StringComparison.Ordinal))
            {
                try
                {
                    var oldAvatarPublicId = _cloudinaryService.GetPublicIdFromUrl(oldConversationAvatar);
                    if (!string.IsNullOrWhiteSpace(oldAvatarPublicId))
                    {
                        await _cloudinaryService.DeleteMediaAsync(oldAvatarPublicId, MediaTypeEnum.Image);
                    }
                }
                catch
                {
                    // Database transaction is already committed; old avatar cleanup should not break the API response.
                }
            }

            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversation.ConversationId);
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            await _realtimeService.NotifyNewMessageAsync(conversation.ConversationId, muteMap, realtimeMessage);

            await _realtimeService.NotifyGroupConversationInfoUpdatedAsync(
                conversation.ConversationId,
                conversation.ConversationName,
                conversation.ConversationAvatar,
                currentId);
        }

        public async Task<PagedResponse<ConversationListItemResponse>> GetConversationsPagedAsync(Guid currentId, bool? isPrivate, string? search, int page, int pageSize)
        {
            var (items, totalCount) = await _conversationRepository.GetConversationsPagedAsync(currentId, isPrivate, search, page, pageSize);

            var responseItems = items.Select(item => new ConversationListItemResponse
            {
                ConversationId = item.ConversationId,
                IsGroup = item.IsGroup,
                DisplayName = item.DisplayName,
                DisplayAvatar = item.DisplayAvatar,
                OtherMember = item.OtherMember != null ? new OtherMemberInfo
                {
                    AccountId = item.OtherMember.AccountId,
                    Username = item.OtherMember.Username,
                    FullName = item.OtherMember.FullName,
                    Nickname = item.OtherMember.Nickname,
                    AvatarUrl = item.OtherMember.AvatarUrl,
                    IsActive = item.OtherMember.IsActive
                } : null,
                LastMessage = item.LastMessage,
                LastMessagePreview = FormatLastMessagePreview(item.LastMessage),
                IsRead = item.IsRead,
                UnreadCount = item.UnreadCount,
                LastMessageSentAt = item.LastMessageSentAt,
                IsMuted = item.IsMuted,
                Theme = item.Theme,
                LastMessageSeenBy = item.LastMessageSeenBy,
                LastMessageSeenCount = item.LastMessageSeenCount
            }).ToList();

            return new PagedResponse<ConversationListItemResponse>(responseItems, page, pageSize, totalCount);
        }

        public async Task<ConversationMessagesResponse> GetConversationMessagesWithMetaDataAsync(Guid conversationId, Guid currentId, string? cursor, int pageSize)
        {
            if (pageSize <= 0) pageSize = 20;

            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
            {
                throw new ForbiddenException("You are not a member of this conversation.");
            }

            var (items, olderCursor, newerCursor, hasMoreOlder, hasMoreNewer) =
                await _messageRepository.GetMessagesByConversationId(conversationId, currentId, cursor, pageSize);

            ConversationMetaData? metaData = null;
            if (string.IsNullOrWhiteSpace(cursor))
            {
                var repoMeta = await _conversationRepository.GetConversationMetaDataAsync(conversationId, currentId);
                if (repoMeta != null)
                {
                    metaData = new ConversationMetaData
                    {
                        ConversationId = repoMeta.ConversationId,
                        IsGroup = repoMeta.IsGroup,
                        IsMuted = repoMeta.IsMuted,
                        Theme = repoMeta.Theme,
                        DisplayName = repoMeta.DisplayName,
                        DisplayAvatar = repoMeta.DisplayAvatar,
                        OtherMember = repoMeta.OtherMember != null ? new OtherMemberInfo
                        {
                            AccountId = repoMeta.OtherMember.AccountId,
                            Username = repoMeta.OtherMember.Username,
                            FullName = repoMeta.OtherMember.FullName,
                            Nickname = repoMeta.OtherMember.Nickname,
                            AvatarUrl = repoMeta.OtherMember.AvatarUrl,
                            IsActive = repoMeta.OtherMember.IsActive
                        } : null
                    };

                    var members = await _conversationMemberRepository.GetConversationMembersAsync(conversationId);
                    metaData.Members = members.Select(m => new ConversationMemberInfo
                    {
                        AccountId = m.AccountId,
                        AvatarUrl = m.Account.AvatarUrl,
                        DisplayName = m.Nickname ?? m.Account.Username,
                        Username = m.Account.Username,
                        Nickname = m.Nickname,
                        Role = m.IsAdmin ? 1 : 0
                    }).ToList();
                    metaData.MemberSeenStatuses = members.Select(m => new MemberSeenStatus
                    {
                        AccountId = m.AccountId,
                        AvatarUrl = m.Account.AvatarUrl,
                        DisplayName = m.Nickname ?? m.Account.Username,
                        LastSeenMessageId = m.LastSeenMessageId
                    }).ToList();
                }
            }

            return new ConversationMessagesResponse
            {
                MetaData = metaData,
                Messages = new CursorResponse<MessageBasicModel>(items, olderCursor, newerCursor, hasMoreOlder, hasMoreNewer)
            };
        }

        public async Task<PrivateConversationIncludeMessagesResponse> GetPrivateConversationWithMessagesByOtherIdAsync(Guid currentId, Guid otherId, string? cursor, int pageSize)
        {
            if (pageSize <= 0) pageSize = 20;

            if (currentId == otherId)
                throw new BadRequestException("You cannot chat with yourself.");

            var conversation = await _conversationRepository.GetConversationByTwoAccountIdsAsync(currentId, otherId);

            if (conversation != null)
            {
                var response = await GetConversationMessagesWithMetaDataAsync(conversation.ConversationId, currentId, cursor, pageSize);
                return new PrivateConversationIncludeMessagesResponse
                {
                    IsNew = false,
                    MetaData = response.MetaData,
                    Messages = response.Messages
                };
            }

            // Case: New Conversation
            var otherAccount = await _accountRepository.GetAccountById(otherId);
            if (otherAccount == null || otherAccount.Status != AccountStatusEnum.Active)
                throw new NotFoundException("Account not found or inactive.");

            return new PrivateConversationIncludeMessagesResponse
            {
                IsNew = true,
                MetaData = new ConversationMetaData
                {
                    ConversationId = Guid.Empty,
                    IsGroup = false,
                    Theme = null,
                    DisplayName = otherAccount.Username, // Initial display name
                    DisplayAvatar = otherAccount.AvatarUrl,
                    OtherMember = new OtherMemberInfo
                    {
                        AccountId = otherAccount.AccountId,
                        Username = otherAccount.Username,
                        FullName = otherAccount.FullName,
                        AvatarUrl = otherAccount.AvatarUrl,
                        IsActive = true
                    }
                },
                Messages = new CursorResponse<MessageBasicModel>(new List<MessageBasicModel>(), null, null, false, false)
            };
        }

        private static string ToMention(string? username)
        {
            var normalized = (username ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "@unknown";
            }

            return normalized.StartsWith("@", StringComparison.Ordinal)
                ? normalized
                : $"@{normalized}";
        }

        private static string BuildGroupInfoSystemMessageFallback(
            string actorUsername,
            string? conversationName,
            bool hasConversationNameChanged,
            bool hasConversationAvatarChanged,
            bool isAvatarRemoved)
        {
            var actorMention = ToMention(actorUsername);

            if (hasConversationNameChanged && hasConversationAvatarChanged)
            {
                var avatarVerb = isAvatarRemoved ? "removed the group avatar" : "updated the group avatar";
                return $"{actorMention} changed the group name to \"{conversationName}\" and {avatarVerb}.";
            }

            if (hasConversationNameChanged)
            {
                return $"{actorMention} changed the group name to \"{conversationName}\".";
            }

            return isAvatarRemoved
                ? $"{actorMention} removed the group avatar."
                : $"{actorMention} updated the group avatar.";
        }

        private string? FormatLastMessagePreview(MessageBasicModel? msg)
        {
            if (msg == null) return null;
            if (msg.IsRecalled) return "Message recalled";

            if (msg.MessageType == MessageTypeEnum.Text)
                return msg.Content;

            if (msg.MessageType == MessageTypeEnum.Media)
            {
                // If media message also has text content, show the text
                if (!string.IsNullOrWhiteSpace(msg.Content))
                    return msg.Content;

                var firstMedia = msg.Medias?.FirstOrDefault();
                if (firstMedia == null) return "Sent a media file";

                return firstMedia.MediaType switch
                {
                    MediaTypeEnum.Image => "[Image]",
                    MediaTypeEnum.Video => "[Video]",
                    MediaTypeEnum.Audio => "[Audio]",
                    _ => "[File]"
                };
            }

            if (msg.MessageType == MessageTypeEnum.System)
                return msg.Content;

            return msg.Content;
        }

        public async Task<int> GetUnreadConversationCountAsync(Guid currentId)
        {
            return await _conversationRepository.GetUnreadConversationCountAsync(currentId);
        }

        public async Task<ConversationMessagesResponse> GetMessageContextAsync(Guid conversationId, Guid currentId, Guid messageId, int pageSize)
        {
            if (pageSize <= 0) pageSize = 20;

            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
            {
                throw new ForbiddenException("You are not a member of this conversation.");
            }

            var position = await _messageRepository.GetMessagePositionAsync(conversationId, currentId, messageId);
            if (position < 0)
            {
                throw new NotFoundException("Message not found in this conversation.");
            }

            var page = (int)Math.Ceiling((double)position / pageSize);
            if (page < 1) page = 1;
            var contextOffsetCursor = ((page - 1) * pageSize).ToString();
            var (items, olderCursor, newerCursor, hasMoreOlder, hasMoreNewer) =
                await _messageRepository.GetMessagesByConversationId(conversationId, currentId, contextOffsetCursor, pageSize);

            // Always include metaData for context loading (frontend needs it when clearing messages)
            ConversationMetaData? metaData = null;
            var repoMeta = await _conversationRepository.GetConversationMetaDataAsync(conversationId, currentId);
            if (repoMeta != null)
            {
                metaData = new ConversationMetaData
                {
                    ConversationId = repoMeta.ConversationId,
                    IsGroup = repoMeta.IsGroup,
                    IsMuted = repoMeta.IsMuted,
                    Theme = repoMeta.Theme,
                    DisplayName = repoMeta.DisplayName,
                    DisplayAvatar = repoMeta.DisplayAvatar,
                    OtherMember = repoMeta.OtherMember != null ? new OtherMemberInfo
                    {
                        AccountId = repoMeta.OtherMember.AccountId,
                        Username = repoMeta.OtherMember.Username,
                        FullName = repoMeta.OtherMember.FullName,
                        Nickname = repoMeta.OtherMember.Nickname,
                        AvatarUrl = repoMeta.OtherMember.AvatarUrl,
                        IsActive = repoMeta.OtherMember.IsActive
                    } : null
                };

                var members = await _conversationMemberRepository.GetConversationMembersAsync(conversationId);
                metaData.Members = members.Select(m => new ConversationMemberInfo
                {
                    AccountId = m.AccountId,
                    AvatarUrl = m.Account.AvatarUrl,
                    DisplayName = m.Nickname ?? m.Account.Username,
                    Username = m.Account.Username,
                    Nickname = m.Nickname,
                    Role = m.IsAdmin ? 1 : 0
                }).ToList();
                metaData.MemberSeenStatuses = members.Select(m => new MemberSeenStatus
                {
                    AccountId = m.AccountId,
                    AvatarUrl = m.Account.AvatarUrl,
                    DisplayName = m.Nickname ?? m.Account.Username,
                    LastSeenMessageId = m.LastSeenMessageId
                }).ToList();
            }

            return new ConversationMessagesResponse
            {
                MetaData = metaData,
                Messages = new CursorResponse<MessageBasicModel>(items, olderCursor, newerCursor, hasMoreOlder, hasMoreNewer)
            };
        }

        public async Task<PagedResponse<MessageBasicModel>> SearchMessagesAsync(Guid conversationId, Guid currentId, string keyword, int page, int pageSize)
        {
            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
            {
                throw new ForbiddenException("You are not a member of this conversation.");
            }

            var (items, totalItems) = await _messageRepository.SearchMessagesAsync(conversationId, currentId, keyword, page, pageSize);

            return new PagedResponse<MessageBasicModel>(items, page, pageSize, totalItems);
        }

        public async Task<List<GroupInviteAccountSearchResponse>> SearchAccountsForGroupInviteAsync(Guid currentId, string keyword, IEnumerable<Guid>? excludeAccountIds, int limit = 10)
        {
            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            if (normalizedKeyword.Length > 0 && normalizedKeyword.Length < 2)
            {
                throw new BadRequestException("Keyword must be empty or at least 2 characters.");
            }

            var items = await _accountRepository.SearchAccountsForGroupInviteAsync(currentId, normalizedKeyword, excludeAccountIds, limit);
            return items.Select(x => new GroupInviteAccountSearchResponse
            {
                AccountId = x.AccountId,
                Username = x.Username,
                FullName = x.FullName,
                AvatarUrl = x.AvatarUrl,
                IsFollowing = x.IsFollowing,
                IsFollower = x.IsFollower,
                MutualGroupCount = x.MutualGroupCount,
                LastDirectMessageAt = x.LastDirectMessageAt,
                MatchScore = x.MatchScore,
                FollowingScore = x.FollowingScore,
                FollowerScore = x.FollowerScore,
                RecentChatScore = x.RecentChatScore,
                MutualGroupScore = x.MutualGroupScore,
                TotalScore = x.TotalScore
            }).ToList();
        }

        public async Task<PagedResponse<ConversationMemberInfo>> GetGroupConversationMembersAsync(
            Guid conversationId,
            Guid currentId,
            int page,
            int pageSize,
            bool adminOnly)
        {
            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
            {
                throw new ForbiddenException("You are not a member of this conversation.");
            }

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
            {
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");
            }

            if (!conversation.IsGroup)
            {
                throw new BadRequestException("Members list is only available for group conversations.");
            }

            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = DefaultGroupMembersPageSize;
            if (pageSize > MaxGroupMembersPageSize) pageSize = MaxGroupMembersPageSize;

            var (members, totalCount) = await _conversationMemberRepository.GetConversationMembersPagedAsync(
                conversationId,
                adminOnly,
                page,
                pageSize);

            var items = members.Select(member => new ConversationMemberInfo
            {
                AccountId = member.AccountId,
                AvatarUrl = member.Account?.AvatarUrl,
                DisplayName = member.Nickname ?? member.Account?.Username,
                Username = member.Account?.Username,
                Nickname = member.Nickname,
                Role = member.IsAdmin ? 1 : 0
            }).ToList();

            return new PagedResponse<ConversationMemberInfo>(items, page, pageSize, totalCount);
        }

        public async Task KickGroupMemberAsync(Guid conversationId, Guid currentId, Guid targetAccountId)
        {
            if (targetAccountId == Guid.Empty)
                throw new BadRequestException("Target account is required.");

            if (currentId == targetAccountId)
                throw new BadRequestException("You cannot kick yourself from the group.");

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Kick is only available for group conversations.");

            var actorMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (actorMember == null)
                throw new ForbiddenException("You are not a member of this conversation.");

            if (!actorMember.IsAdmin)
                throw new ForbiddenException("Only group admins can kick members.");

            var targetMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, targetAccountId);
            if (targetMember == null)
                throw new BadRequestException("Target account is not an active member of this group.");

            if (targetMember.IsAdmin)
                throw new BadRequestException("You cannot kick another admin.");

            var accounts = await _accountRepository.GetAccountsByIds(new[] { currentId, targetAccountId });
            var actor = accounts.FirstOrDefault(a => a.AccountId == currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var target = accounts.FirstOrDefault(a => a.AccountId == targetAccountId);
            if (target == null)
                throw new NotFoundException($"Account with ID {targetAccountId} does not exist.");

            targetMember.HasLeft = true;
            var sentAt = DateTime.UtcNow;
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildMemberKickedSystemMessageFallback(actor.Username, target.Username),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.MemberKicked,
                    actorAccountId = currentId,
                    actorUsername = actor.Username,
                    targetAccountId,
                    targetUsername = target.Username
                }),
                SentAt = sentAt,
                IsEdited = false,
                IsRecalled = false
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _conversationMemberRepository.UpdateConversationMember(targetMember);
                await _messageRepository.AddMessageAsync(systemMessage);
                return true;
            });

            await _realtimeService.NotifyConversationRemovedAsync(targetAccountId, conversationId, "kicked");

            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        public async Task AssignGroupAdminAsync(Guid conversationId, Guid currentId, Guid targetAccountId)
        {
            if (targetAccountId == Guid.Empty)
                throw new BadRequestException("Target account is required.");

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Assign admin is only available for group conversations.");

            var actorMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (actorMember == null)
                throw new ForbiddenException("You are not a member of this conversation.");

            if (!actorMember.IsAdmin)
                throw new ForbiddenException("Only group admins can assign another admin.");

            var targetMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, targetAccountId);
            if (targetMember == null)
                throw new BadRequestException("Target account is not an active member of this group.");

            if (targetMember.IsAdmin)
                throw new BadRequestException("Target member is already an admin.");

            targetMember.IsAdmin = true;

            var accounts = await _accountRepository.GetAccountsByIds(new[] { currentId, targetAccountId });
            var actor = accounts.FirstOrDefault(a => a.AccountId == currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var target = accounts.FirstOrDefault(a => a.AccountId == targetAccountId);
            if (target == null)
                throw new NotFoundException($"Account with ID {targetAccountId} does not exist.");

            var sentAt = DateTime.UtcNow;
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildAdminGrantedSystemMessageFallback(actor.Username, target.Username),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.AdminGranted,
                    actorAccountId = currentId,
                    actorUsername = actor.Username,
                    targetAccountId,
                    targetUsername = target.Username
                }),
                SentAt = sentAt,
                IsEdited = false,
                IsRecalled = false
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _conversationMemberRepository.UpdateConversationMember(targetMember);
                await _messageRepository.AddMessageAsync(systemMessage);
                return true;
            });

            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        public async Task LeaveGroupAsync(Guid conversationId, Guid currentId)
        {
            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Leave is only available for group conversations.");

            var actorMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (actorMember == null)
                throw new ForbiddenException("You are not a member of this conversation.");

            var activeMembers = await _conversationMemberRepository.GetConversationMembersAsync(conversationId);
            if (activeMembers.Count == 0)
                throw new BadRequestException("This group has no active members.");

            var actor = activeMembers
                .FirstOrDefault(member => member.AccountId == currentId)
                ?.Account;
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var activeAdminCount = activeMembers.Count(member => member.IsAdmin);
            var activeMemberCount = activeMembers.Count;
            if (actorMember.IsAdmin && activeMemberCount > 1 && activeAdminCount <= 1)
                throw new BadRequestException("You are the only admin. Assign another admin before leaving the group.");

            actorMember.HasLeft = true;
            var sentAt = DateTime.UtcNow;
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildMemberLeftSystemMessageFallback(actor.Username),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.MemberLeft,
                    actorAccountId = currentId,
                    actorUsername = actor.Username
                }),
                SentAt = sentAt,
                IsEdited = false,
                IsRecalled = false
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _conversationMemberRepository.UpdateConversationMember(actorMember);
                await _messageRepository.AddMessageAsync(systemMessage);
                return true;
            });

            await _realtimeService.NotifyConversationRemovedAsync(currentId, conversationId, "left");

            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        public async Task<PagedResponse<ConversationMediaItemModel>> GetConversationMediaAsync(Guid conversationId, Guid currentId, int page, int pageSize)
        {
            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
            {
                throw new ForbiddenException("You are not a member of this conversation.");
            }

            var (items, totalItems) = await _messageRepository.GetConversationMediaAsync(conversationId, currentId, page, pageSize);
            return new PagedResponse<ConversationMediaItemModel>(items, page, pageSize, totalItems);
        }

        public async Task<PagedResponse<ConversationMediaItemModel>> GetConversationFilesAsync(Guid conversationId, Guid currentId, int page, int pageSize)
        {
            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
            {
                throw new ForbiddenException("You are not a member of this conversation.");
            }

            var (items, totalItems) = await _messageRepository.GetConversationFilesAsync(conversationId, currentId, page, pageSize);
            return new PagedResponse<ConversationMediaItemModel>(items, page, pageSize, totalItems);
        }

        private static string BuildMemberKickedSystemMessageFallback(string actorUsername, string targetUsername)
        {
            var actorMention = ToMention(actorUsername);
            var targetMention = ToMention(targetUsername);
            return $"{actorMention} removed {targetMention} from the group.";
        }

        private static string BuildMemberLeftSystemMessageFallback(string actorUsername)
        {
            var actorMention = ToMention(actorUsername);
            return $"{actorMention} left the group.";
        }

        private static string BuildAdminGrantedSystemMessageFallback(string actorUsername, string targetUsername)
        {
            var actorMention = ToMention(actorUsername);
            var targetMention = ToMention(targetUsername);
            return $"{actorMention} made {targetMention} an admin.";
        }
    }
}
