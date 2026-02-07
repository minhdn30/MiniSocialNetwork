using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace SocialNetwork.API.Hubs
{
    public class PostHub : Hub
    {
        // detail view group
        public async Task JoinPostGroup(Guid postId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Post-{postId}");
        }

        public async Task LeavePostGroup(Guid postId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Post-{postId}");
        }

        // list view / personal feed group
        public async Task JoinPostListGroup(Guid accountId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"PostList-{accountId}");
        }

        public async Task LeavePostListGroup(Guid accountId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"PostList-{accountId}");
        }
    }

}
