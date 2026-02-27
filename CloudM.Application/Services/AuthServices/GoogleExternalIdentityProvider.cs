using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using CloudM.Domain.Enums;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.AuthServices
{
    public class GoogleExternalIdentityProvider : IExternalIdentityProvider
    {
        private readonly GoogleAuthOptions _options;

        public GoogleExternalIdentityProvider(IOptions<GoogleAuthOptions> options)
        {
            _options = options?.Value ?? new GoogleAuthOptions();
        }

        public ExternalLoginProviderEnum Provider => ExternalLoginProviderEnum.Google;

        public async Task<ExternalAuthIdentity> VerifyAsync(string credential)
        {
            var validationSettings = new GoogleJsonWebSignature.ValidationSettings();
            var allowedClientIds = _options.AllowedClientIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (allowedClientIds.Count > 0)
            {
                validationSettings.Audience = allowedClientIds;
            }

            var payload = await GoogleJsonWebSignature.ValidateAsync(credential, validationSettings);
            if (payload == null || string.IsNullOrWhiteSpace(payload.Subject))
            {
                throw new UnauthorizedException("Invalid Google credential.");
            }

            var normalizedEmail = (payload.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                throw new UnauthorizedException("Google account email is unavailable.");
            }

            return new ExternalAuthIdentity
            {
                Provider = Provider,
                ProviderUserId = payload.Subject.Trim(),
                Email = normalizedEmail,
                EmailVerified = payload.EmailVerified,
                FullName = string.IsNullOrWhiteSpace(payload.Name) ? null : payload.Name.Trim(),
                AvatarUrl = string.IsNullOrWhiteSpace(payload.Picture) ? null : payload.Picture.Trim(),
            };
        }
    }
}
