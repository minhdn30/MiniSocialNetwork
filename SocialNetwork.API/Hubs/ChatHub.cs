using Microsoft.AspNetCore.SignalR;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.ConversationMemberServices;
using SocialNetwork.Application.Services.ConversationServices;

namespace SocialNetwork.API.Hubs
{
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
        public async Task JoinConversation(Guid conversationId)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                conversationId.ToString()
            );
        }

        public async Task LeaveConversation(Guid conversationId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                conversationId.ToString()
            );
        }
        public async Task SeenConversation(Guid conversationId, Guid lastSeenMessageId)
        {
            var currentId = Context.User?.GetAccountId()
                ?? throw new HubException("Unauthorized");

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
            var currentId = Context.User?.GetAccountId()
                ?? throw new HubException("Unauthorized");

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
