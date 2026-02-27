using CloudM.Domain.Enums;

namespace CloudM.Application.Services.AuthServices
{
    public class ExternalAuthIdentity
    {
        public ExternalLoginProviderEnum Provider { get; set; }
        public string ProviderUserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public string? FullName { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
