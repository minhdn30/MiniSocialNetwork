using CloudM.Domain.Enums;

namespace CloudM.Application.Services.AuthServices
{
    public interface IExternalIdentityProvider
    {
        ExternalLoginProviderEnum Provider { get; }
        Task<ExternalAuthIdentity> VerifyAsync(string credential);
    }
}
