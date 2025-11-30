using SocialNetwork.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.EmailVerifications
{
    public interface IEmailVerificationRepository
    {
        Task AddEmailVerificationAsync(EmailVerification emailVerification);
        Task UpdateEmailVerificationAsync(EmailVerification emailVerification);
        Task<bool> IsEmailExist(string email);
        Task<bool> VerifyCodeAsync(string email, string code);
        Task DeleteEmailVerificationAsync(string email);
        
     }
}
