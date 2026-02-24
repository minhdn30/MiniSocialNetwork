using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using SocialNetwork.Application.DTOs.ConversationMemberDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.Helpers.StoryHelpers;
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
using SocialNetwork.Application.Services.StoryServices;
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
        private readonly IStoryRingStateHelper _storyRingStateHelper;
        public ConversationService(IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IMessageRepository messageRepository, IAccountRepository accountRepository, IFollowRepository followRepository,
            IUnitOfWork unitOfWork, ICloudinaryService cloudinaryService, IMapper mapper, IRealtimeService realtimeService,
            IStoryService? storyService = null, IStoryRingStateHelper? storyRingStateHelper = null)
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
            _storyRingStateHelper = storyRingStateHelper ?? new StoryRingStateHelper(storyService);
        }
        public async Task<ConversationResponse?> GetPrivateConversationAsync(Guid currentId, Guid otherId)
        {
            var conversation = await _conversationRepository.GetConversationByTwoAccountIdsAsync(currentId, otherId);
            return conversation == null ? null : _mapper.Map<ConversationResponse>(conversation);         
        }
        public async Task<ConversationResponse> CreatePrivateConversationAsync(Guid currentId, Guid otherId)
        {
            if(!await _accountRepository.IsAccountIdExist(currentId) || !await _accountRepository.IsAccountIdExist(otherId))
                throw new NotFoundException("One or both accounts do not exist.");
            if(await _conversationRepository.IsPrivateConversationExistBetweenTwoAccounts(currentId, otherId))
                throw new BadRequestException("A private conversation between these two accounts already exists.");
            var conversation = await _conversationRepository.CreatePrivateConversationAsync(currentId, otherId);
            return _mapper.Map<ConversationResponse>(conversation);
        }

        public async Task<ConversationResponse> CreateGroupConversationAsync(Guid currentId, CreateGroupConversationRequest request)
        {
            var normalizedGroupName = request.GroupName?.Trim();

            var uniqueOtherMemberIds = (request.MemberIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty && id != currentId)
                .Distinct()
                .ToList();

            var totalMembers = uniqueOtherMemberIds.Count + 1;

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
                Owner = currentId,
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
                Owner = conversation.Owner,
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
            var actorMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (actorMember == null)
                throw new ForbiddenException("You are not a member of this conversation.");

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Only group conversations can be updated.");

            var ownerId = conversation.Owner ?? (conversation.CreatedBy != Guid.Empty ? conversation.CreatedBy : Guid.Empty);
            var actorIsOwner = ownerId != Guid.Empty && ownerId == currentId;
            if (!actorMember.IsAdmin && !actorIsOwner)
                throw new ForbiddenException("Only group admins can edit group name or avatar.");

            var actor = await _accountRepository.GetAccountById(currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var hasConversationNameInput = request.ConversationName != null;
            var normalizedConversationName = request.ConversationName?.Trim();

            var hasConversationNameChanged =
                hasConversationNameInput &&
                !string.IsNullOrWhiteSpace(normalizedConversationName) &&
                !string.Equals(conversation.ConversationName, normalizedConversationName, StringComparison.Ordinal);

            var hasConversationAvatarInput = request.ConversationAvatar != null;
            var hasRemoveConversationAvatarInput = request.RemoveAvatar;

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
                conversation.Owner,
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
                Owner = item.Owner,
                CurrentUserRole = item.CurrentUserRole,
                GroupAvatars = item.GroupAvatars,
                LastMessageSeenBy = item.LastMessageSeenBy,
                LastMessageSeenCount = item.LastMessageSeenCount
            }).ToList();

            await ApplyStoryRingForOtherMembersAsync(currentId, responseItems.Select(x => x.OtherMember));

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
                        Owner = repoMeta.Owner,
                        CurrentUserRole = repoMeta.CurrentUserRole,
                        GroupAvatars = repoMeta.GroupAvatars,
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

                    await ApplyStoryRingForOtherMemberAsync(currentId, metaData.OtherMember);

                    var members = await _conversationMemberRepository.GetConversationMembersAsync(conversationId);
                    var ownerId = metaData.Owner;
                    metaData.Members = members.Select(m => new ConversationMemberInfo
                    {
                        AccountId = m.AccountId,
                        AvatarUrl = m.Account.AvatarUrl,
                        DisplayName = m.Nickname ?? m.Account.Username,
                        Username = m.Account.Username,
                        Nickname = m.Nickname,
                        Role = (m.IsAdmin || (ownerId.HasValue && ownerId.Value == m.AccountId)) ? 1 : 0
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
                        IsActive = true,
                        StoryRingState = await ResolveStoryRingStateAsync(currentId, otherAccount.AccountId)
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
                    Owner = repoMeta.Owner,
                    CurrentUserRole = repoMeta.CurrentUserRole,
                    GroupAvatars = repoMeta.GroupAvatars,
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

                await ApplyStoryRingForOtherMemberAsync(currentId, metaData.OtherMember);

                var members = await _conversationMemberRepository.GetConversationMembersAsync(conversationId);
                var ownerId = metaData.Owner;
                metaData.Members = members.Select(m => new ConversationMemberInfo
                {
                    AccountId = m.AccountId,
                    AvatarUrl = m.Account.AvatarUrl,
                    DisplayName = m.Nickname ?? m.Account.Username,
                    Username = m.Account.Username,
                    Nickname = m.Nickname,
                    Role = (m.IsAdmin || (ownerId.HasValue && ownerId.Value == m.AccountId)) ? 1 : 0
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

        private async Task ApplyStoryRingForOtherMembersAsync(Guid currentId, IEnumerable<OtherMemberInfo?> otherMembers)
        {
            var targets = (otherMembers ?? Enumerable.Empty<OtherMemberInfo?>())
                .Where(x => x != null && x.AccountId != Guid.Empty)
                .Cast<OtherMemberInfo>()
                .ToList();

            if (targets.Count == 0)
            {
                return;
            }

            var targetIds = targets
                .Select(x => x.AccountId)
                .Distinct()
                .ToList();

            var stateMap = await _storyRingStateHelper.ResolveManyAsync(currentId, targetIds);

            foreach (var member in targets)
            {
                member.StoryRingState = stateMap.TryGetValue(member.AccountId, out var ringState)
                    ? ringState
                    : StoryRingStateEnum.None;
            }
        }

        private async Task ApplyStoryRingForOtherMemberAsync(Guid currentId, OtherMemberInfo? otherMember)
        {
            if (otherMember == null || otherMember.AccountId == Guid.Empty)
            {
                return;
            }

            otherMember.StoryRingState = await ResolveStoryRingStateAsync(currentId, otherMember.AccountId);
        }

        private async Task<StoryRingStateEnum> ResolveStoryRingStateAsync(Guid currentId, Guid targetAccountId)
        {
            return await _storyRingStateHelper.ResolveAsync(currentId, targetAccountId);
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

            var ownerId = conversation.Owner ?? (conversation.CreatedBy != Guid.Empty ? conversation.CreatedBy : Guid.Empty);
            var items = members.Select(member => new ConversationMemberInfo
            {
                AccountId = member.AccountId,
                AvatarUrl = member.Account?.AvatarUrl,
                DisplayName = member.Nickname ?? member.Account?.Username,
                Username = member.Account?.Username,
                Nickname = member.Nickname,
                Role = (member.IsAdmin || (ownerId != Guid.Empty && ownerId == member.AccountId)) ? 1 : 0
            }).ToList();

            return new PagedResponse<ConversationMemberInfo>(items, page, pageSize, totalCount);
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

    }
}
