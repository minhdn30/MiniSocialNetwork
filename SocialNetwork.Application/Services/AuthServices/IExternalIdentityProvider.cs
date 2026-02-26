using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Application.Services.AuthServices
{
    public interface IExternalIdentityProvider
    {
        ExternalLoginProviderEnum Provider { get; }
        Task<ExternalAuthIdentity> VerifyAsync(string credential);
    }
}
