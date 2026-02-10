using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace SocialNetwork.API.Hubs
{
    [Authorize]
    public class UserHub : Hub
    {
        public async Task JoinAccountGroup(Guid accountId)
        {
            if (Context.User?.Identity?.IsAuthenticated != true)
                throw new HubException("Unauthorized");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Account-{accountId}");
        }

        public async Task LeaveAccountGroup(Guid accountId)
        {
            if (Context.User?.Identity?.IsAuthenticated != true)
                throw new HubException("Unauthorized");
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Account-{accountId}");
        }
    }

}
