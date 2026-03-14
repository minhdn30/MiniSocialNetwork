using Microsoft.AspNetCore.Http;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using CloudM.Infrastructure.Repositories.Accounts;
using System.Security.Claims;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.API.Middleware
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

                var isAdminPath = path.StartsWith("/api/admin/");

                var account = await accountRepository.GetAccountById(accountId);
                if (account != null && account.Status != AccountStatusEnum.Active)
                {
                    throw new ForbiddenException($"Your account is currently {account.Status}. Please reactivate or contact support.");
                }

                if (account != null && !isAdminPath && !SocialRoleRules.IsSocialEligible(account))
                {
                    throw new ForbiddenException("This account cannot access social features.");
                }
            }

            await _next(context);
        }
    }
}
