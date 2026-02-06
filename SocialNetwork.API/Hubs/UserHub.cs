using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace SocialNetwork.API.Hubs
{
    public class UserHub : Hub
    {
        public async Task JoinAccountGroup(Guid accountId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Account-{accountId}");
        }

        public async Task LeaveAccountGroup(Guid accountId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Account-{accountId}");
        }
    }

}
