using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace SocialNetwork.API.Services
{
    public class SignalRAccountIdUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            var user = connection.User;
            if (user == null) return null;

            // Prefer explicit AccountId claim, fallback to standard NameIdentifier.
            var accountId = user.FindFirst("AccountId")?.Value;
            if (!string.IsNullOrWhiteSpace(accountId))
            {
                return accountId;
            }

            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
