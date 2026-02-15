using Microsoft.AspNetCore.Http;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using System.Security.Claims;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.API.Middleware
{
    public class AccountStatusMiddleware
    {
        private readonly RequestDelegate _next;

        public AccountStatusMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IAccountRepository accountRepository)
        {
            var accountIdClaim = context.User.FindFirst("AccountId")?.Value;

            if (Guid.TryParse(accountIdClaim, out var accountId))
            {
                var path = context.Request.Path.Value?.ToLower() ?? "";

                // Exclude paths that should be accessible even if status is not Active
                if (path.Contains("/accounts/reactivate") || path.Contains("/auths/"))
                {
                    await _next(context);
                    return;
                }

                var account = await accountRepository.GetAccountById(accountId);
                if (account != null && account.Status != AccountStatusEnum.Active)
                {
                    throw new ForbiddenException($"Your account is currently {account.Status}. Please reactivate or contact support.");
                }
            }

            await _next(context);
        }
    }
}
