using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Helpers.ClaimHelpers
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid? GetAccountId(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (claim != null && Guid.TryParse(claim, out var id))
                return id;

            return null;
        }

        public static string? GetUsername(this ClaimsPrincipal user) =>
            user.FindFirst(ClaimTypes.Name)?.Value;

        public static string? GetFullName(this ClaimsPrincipal user) =>
            user.FindFirst("fullName")?.Value;

        public static string? GetAvatarUrl(this ClaimsPrincipal user) =>
            user.FindFirst("avatarUrl")?.Value;

        public static bool? IsVerified(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst("isVerified")?.Value;
            if (claim == null) return null;
            return bool.TryParse(claim, out var result) ? result : (bool?)null;
        }

        public static string? GetRole(this ClaimsPrincipal user) =>
            user.FindFirst(ClaimTypes.Role)?.Value;

        public static string? GetEmail(this ClaimsPrincipal user) =>
            user.FindFirst(ClaimTypes.Email)?.Value;

        //check isAdmin
        public static bool IsAdmin(this ClaimsPrincipal user)
        {
            var role = user.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<RoleEnum>(role, ignoreCase: true, out var parsed) && parsed == RoleEnum.Admin;
        }

    }
}
