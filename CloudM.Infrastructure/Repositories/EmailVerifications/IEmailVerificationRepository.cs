using CloudM.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.EmailVerifications
{
    public interface IEmailVerificationRepository
    {
        Task<EmailVerification?> GetLatestByEmailAsync(string email);
        Task EnsureExistsByEmailAsync(string email, DateTime nowUtc);
        Task AddEmailVerificationAsync(EmailVerification emailVerification);
        Task UpdateEmailVerificationAsync(EmailVerification emailVerification);
        Task DeleteEmailVerificationAsync(string email);
        Task<bool> TryMarkAsConsumedAsync(int verificationId, DateTime consumedAtUtc);
        Task<int> CleanupStaleVerificationsAsync(DateTime nowUtc, DateTime createdBeforeUtc);
     }
}
