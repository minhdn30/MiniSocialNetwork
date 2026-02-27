using CloudM.Domain.Entities;
using CloudM.Domain.Enums;

namespace CloudM.Infrastructure.Repositories.ExternalLogins
{
    public interface IExternalLoginRepository
    {
        Task<ExternalLogin?> GetByProviderUserIdAsync(ExternalLoginProviderEnum provider, string providerUserId);
        Task<ExternalLogin?> GetByAccountIdAndProviderAsync(Guid accountId, ExternalLoginProviderEnum provider);
        Task<List<ExternalLogin>> GetByAccountIdAsync(Guid accountId);
        Task<int> CountByAccountIdAsync(Guid accountId);
        Task AddAsync(ExternalLogin externalLogin);
        Task DeleteAsync(ExternalLogin externalLogin);
    }
}
