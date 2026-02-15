using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.ConversationMemberServices;
using SocialNetwork.Application.Services.ConversationServices;

namespace SocialNetwork.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IConversationMemberService _conversationMemberService;

        public ChatHub(IConversationMemberService conversationMemberService)
        {
            _conversationMemberService = conversationMemberService;
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }
        private async Task<Guid> EnsureMemberAsync(Guid conversationId)
        {
            var currentId = Context.User?.GetAccountId()
                ?? throw new HubException("Unauthorized");

            var isMember = await _conversationMemberService.IsMemberAsync(conversationId, currentId);
            if (!isMember)
                throw new HubException("Forbidden");

            return currentId;
        }

        public async Task JoinConversation(Guid conversationId)
        {
            await EnsureMemberAsync(conversationId);
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                conversationId.ToString()
            );
        }

        public async Task LeaveConversation(Guid conversationId)
        {
            await EnsureMemberAsync(conversationId);
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                conversationId.ToString()
            );
        }
        public async Task SeenConversation(Guid conversationId, Guid lastSeenMessageId)
        {
            var currentId = await EnsureMemberAsync(conversationId);

            await _conversationMemberService.MarkSeenAsync(
                conversationId,
                currentId,
                lastSeenMessageId);

            await Clients
                .GroupExcept(conversationId.ToString(), Context.ConnectionId)
                .SendAsync("MemberSeen", new
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    LastSeenMessageId = lastSeenMessageId
                });
        }

        public async Task Typing(Guid conversationId, bool isTyping)
        {
            var currentId = await EnsureMemberAsync(conversationId);

            await Clients
                .GroupExcept(conversationId.ToString(), Context.ConnectionId)
                .SendAsync("Typing", new
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    IsTyping = isTyping
                });
        }

    }
}
