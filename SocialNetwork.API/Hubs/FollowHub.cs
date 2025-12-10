using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace SocialNetwork.API.Hubs
{
    public class FollowHub : Hub
    {
        public async Task JoinUserGroup(Guid accountId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Account-{accountId}");
        }

        public async Task LeaveUserGroup(Guid accountId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Account-{accountId}");
        }
    }

}
