using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.Services.RealtimeServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.MessageReacts;
using SocialNetwork.Infrastructure.Repositories.Messages;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Linq;
using System.Threading.Tasks;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.MessageReactServices
{
    public class MessageReactService : IMessageReactService
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IMessageReactRepository _messageReactRepository;
        private readonly IConversationMemberRepository _conversationMemberRepository;
        private readonly IRealtimeService _realtimeService;
        private readonly IUnitOfWork _unitOfWork;

        public MessageReactService(
            IMessageRepository messageRepository,
            IMessageReactRepository messageReactRepository,
            IConversationMemberRepository conversationMemberRepository,
            IRealtimeService realtimeService,
            IUnitOfWork unitOfWork)
        {
            _messageRepository = messageRepository;
            _messageReactRepository = messageReactRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
        }

        public async Task<MessageReactStateResponse> GetMessageReactStateAsync(Guid messageId, Guid accountId)
        {
            var message = await EnsureCanAccessMessageAsync(messageId, accountId);
            return await BuildMessageReactStateAsync(message.MessageId, message.ConversationId, accountId);
        }

        public async Task<MessageReactStateResponse> SetMessageReactAsync(Guid messageId, Guid accountId, ReactEnum reactType)
        {
            var message = await EnsureCanAccessMessageAsync(messageId, accountId);

            var existingReact = await _messageReactRepository.GetReactAsync(messageId, accountId);
            if (existingReact == null)
            {
                await _messageReactRepository.AddReactAsync(new MessageReact
                {
                    MessageId = messageId,
                    AccountId = accountId,
                    ReactType = reactType,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else if (existingReact.ReactType == reactType)
            {
                await _messageReactRepository.RemoveReactAsync(messageId, accountId);
            }
            else
            {
                existingReact.ReactType = reactType;
                await _messageReactRepository.UpdateReactAsync(existingReact);
            }

            await _unitOfWork.CommitAsync();
            await _realtimeService.NotifyMessageReactUpdatedAsync(message.ConversationId, messageId, accountId);
            return await BuildMessageReactStateAsync(message.MessageId, message.ConversationId, accountId);
        }

        //Use only when SetMessageReactAsync cannot be removed
        public async Task<MessageReactStateResponse> RemoveMessageReactAsync(Guid messageId, Guid accountId)
        {
            var message = await EnsureCanAccessMessageAsync(messageId, accountId);

            var existingReact = await _messageReactRepository.GetReactAsync(messageId, accountId);
            if (existingReact != null)
            {
                await _messageReactRepository.RemoveReactAsync(messageId, accountId);
                await _unitOfWork.CommitAsync();
                await _realtimeService.NotifyMessageReactUpdatedAsync(message.ConversationId, messageId, accountId);
            }

            return await BuildMessageReactStateAsync(message.MessageId, message.ConversationId, accountId);
        }

        private async Task<MessageReactStateResponse> BuildMessageReactStateAsync(Guid messageId, Guid conversationId, Guid accountId)
        {
            var reacts = (await _messageReactRepository.GetReactsByMessageIdAsync(messageId))
                .ToList();

            var memberNicknames = await _conversationMemberRepository.GetConversationMembersAsync(conversationId);
            var nicknameMap = memberNicknames
                .GroupBy(x => x.AccountId)
                .ToDictionary(g => g.Key, g => g.First().Nickname);

            var reactedBy = reacts
                .Select(r => new MessageReactAccountModel
                {
                    AccountId = r.AccountId,
                    Username = r.Account?.Username,
                    FullName = r.Account?.FullName,
                    AvatarUrl = r.Account?.AvatarUrl,
                    Nickname = nicknameMap.TryGetValue(r.AccountId, out var nickname) ? nickname : null,
                    ReactType = r.ReactType,
                    CreatedAt = r.CreatedAt
                })
                .OrderByDescending(x => x.AccountId == accountId)
                .ThenBy(x => x.CreatedAt)
                .ToList();

            var currentUserReact = reactedBy.FirstOrDefault(x => x.AccountId == accountId);
            var summary = reactedBy
                .GroupBy(x => x.ReactType)
                .Select(g => new MessageReactSummaryModel
                {
                    ReactType = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.ReactType)
                .ToList();

            return new MessageReactStateResponse
            {
                MessageId = messageId,
                ConversationId = conversationId,
                IsReacted = currentUserReact != null,
                CurrentUserReactType = currentUserReact?.ReactType,
                TotalReacts = reactedBy.Count,
                Reacts = summary,
                ReactedBy = reactedBy
            };
        }

        private async Task<Message> EnsureCanAccessMessageAsync(Guid messageId, Guid accountId)
        {
            var message = await _messageRepository.GetMessageByIdAsync(messageId);
            if (message == null)
            {
                throw new NotFoundException("Message not found.");
            }

            var isMember = await _conversationMemberRepository.IsMemberOfConversation(message.ConversationId, accountId);
            if (!isMember)
            {
                throw new ForbiddenException("You are not allowed to access this message.");
            }

            return message;
        }
    }
}
