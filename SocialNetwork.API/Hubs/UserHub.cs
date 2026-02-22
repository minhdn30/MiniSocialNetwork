using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.PresenceServices;

namespace SocialNetwork.API.Hubs
{
    [Authorize]
    public class UserHub : Hub
    {
        private readonly IOnlinePresenceService _onlinePresenceService;
        private readonly ILogger<UserHub> _logger;

        public UserHub(IOnlinePresenceService onlinePresenceService, ILogger<UserHub> logger)
        {
            _onlinePresenceService = onlinePresenceService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var accountId = Context.User?.GetAccountId();
                if (accountId.HasValue && accountId.Value != Guid.Empty)
                {
                    await _onlinePresenceService.MarkConnectedAsync(
                        accountId.Value,
                        Context.ConnectionId,
                        DateTime.UtcNow,
                        Context.ConnectionAborted);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UserHub OnConnected presence hook failed.");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var accountId = Context.User?.GetAccountId();
                await _onlinePresenceService.MarkDisconnectedAsync(
                    accountId,
                    Context.ConnectionId,
                    DateTime.UtcNow,
                    Context.ConnectionAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UserHub OnDisconnected presence hook failed.");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task Heartbeat()
        {
            var accountId = Context.User?.GetAccountId();
            if (!accountId.HasValue || accountId.Value == Guid.Empty)
            {
                throw new HubException("Unauthorized");
            }

            await _onlinePresenceService.TouchHeartbeatAsync(
                accountId.Value,
                Context.ConnectionId,
                DateTime.UtcNow,
                Context.ConnectionAborted);
        }

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
