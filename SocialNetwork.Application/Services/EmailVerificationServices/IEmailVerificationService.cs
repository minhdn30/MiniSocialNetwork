using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.EmailVerificationServices
{
    public interface IEmailVerificationService
    {
        Task SendVerificationEmailAsync(string email, string? requesterIpAddress = null);
        Task<bool> VerifyEmailAsync(string email, string code);
    }
}
