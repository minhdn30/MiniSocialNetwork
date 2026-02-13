using AutoMapper;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.ConversationServices
{
    public class ConversationService : IConversationService
    {
        private readonly IConversationRepository _conversationRepository;
        private readonly IConversationMemberRepository _conversationMemberRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        public ConversationService(IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IMessageRepository messageRepository, IAccountRepository accountRepository, IMapper mapper)
        {
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _messageRepository = messageRepository;
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
                LastMessageSeenBy = item.LastMessageSeenBy,
                LastMessageSeenCount = item.LastMessageSeenCount
            }).ToList();

            return new PagedResponse<ConversationListItemResponse>(responseItems, page, pageSize, totalCount);
        }

        public async Task<ConversationMessagesResponse> GetConversationMessagesWithMetaDataAsync(Guid conversationId, Guid currentId, int page, int pageSize)
        {
            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
            {
                throw new ForbiddenException("You are not a member of this conversation.");
            }

            var (messages, totalItems) = await _messageRepository.GetMessagesByConversationId(conversationId, currentId, page, pageSize);

            ConversationMetaData? metaData = null;
            if (page == 1)
            {
                var repoMeta = await _conversationRepository.GetConversationMetaDataAsync(conversationId, currentId);
                if (repoMeta != null)
                {
                    metaData = new ConversationMetaData
                    {
                        ConversationId = repoMeta.ConversationId,
                        IsGroup = repoMeta.IsGroup,
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
                Messages = new PagedResponse<MessageBasicModel>
                {
                    Items = messages,
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = totalItems
                }
            };
        }

        public async Task<PrivateConversationIncludeMessagesResponse> GetPrivateConversationWithMessagesByOtherIdAsync(Guid currentId, Guid otherId, int page, int pageSize)
        {
            if (currentId == otherId)
                throw new BadRequestException("You cannot chat with yourself.");

            var conversation = await _conversationRepository.GetConversationByTwoAccountIdsAsync(currentId, otherId);

            if (conversation != null)
            {
                var response = await GetConversationMessagesWithMetaDataAsync(conversation.ConversationId, currentId, page, pageSize);
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
                Messages = new PagedResponse<MessageBasicModel>
                {
                    Items = new List<MessageBasicModel>(),
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = 0
                }
            };
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
    }
}
