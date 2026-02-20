using SocialNetwork.Domain.Entities;

namespace SocialNetwork.Infrastructure.Repositories.EmailVerificationIpRateLimits
{
    public interface IEmailVerificationIpRateLimitRepository
    {
        Task<EmailVerificationIpRateLimit?> GetByIpAsync(string ipAddress);
        Task EnsureExistsByIpAsync(string ipAddress, DateTime nowUtc);
        Task AddAsync(EmailVerificationIpRateLimit ipRateLimit);
        Task UpdateAsync(EmailVerificationIpRateLimit ipRateLimit);
        Task<int> CleanupStaleAsync(DateTime nowUtc, DateTime updatedBeforeUtc);
    }
}
