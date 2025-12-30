using Microsoft.AspNetCore.SignalR;
using SocialNetwork.Application.Helpers.ClaimHelpers;

namespace SocialNetwork.API.Hubs
{
    public class ChatHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.GetAccountId();
            if (userId != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, userId.Value.ToString());
            }
            await base.OnConnectedAsync();
        }
    }
}
