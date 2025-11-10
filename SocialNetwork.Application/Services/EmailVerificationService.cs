using SocialNetwork.Application.Interfaces;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.EmailVerifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services
{
    public class EmailVerificationService : IEmailVerificationService
    {
        private readonly IEmailService _emailService;
        private readonly IAccountRepository _accountRepository;
        private readonly IEmailVerificationRepository _emailVerificationRepository;
        public EmailVerificationService(IEmailService emailService, IEmailVerificationRepository emailVerificationRepository, IAccountRepository accountRepository)
        {
            _emailService = emailService;
            _emailVerificationRepository = emailVerificationRepository;
            _accountRepository = accountRepository;
        }
        public async Task SendVerificationEmailAsync(string email)
        {
            var code = new Random().Next(100000, 999999).ToString();
            var verification = new EmailVerification
            {
                Email = email,
                Code = code,
                ExpiredAt = DateTime.UtcNow.AddMinutes(5)
            };
            if(await _emailVerificationRepository.IsEmailExist(email))
            {
                // Update existing verification
                await _emailVerificationRepository.UpdateEmailVerificationAsync(verification);
            }
            else
            {
                // Add new verification
                await _emailVerificationRepository.AddEmailVerificationAsync(verification);
            }
            await _emailService.SendEmailAsync(email, "Email Verification", $"Your verification code is: {code}. The code is valid for 5 minutes.");
        }

        public async Task<bool> VerifyEmailAsync(string email, string code)
        {
            var isValid = await _emailVerificationRepository.VerifyCodeAsync(email, code);
            if(!isValid)
            {
                return false;
            }
            await _emailVerificationRepository.DeleteEmailVerificationAsync(email, code);
            var account  = await _accountRepository.GetAccountByEmail(email);
            if(account != null)
            {
                account.IsEmailVerified = true;
                await _accountRepository.UpdateAccount(account);
            }
            return true;
        }
    }
}
