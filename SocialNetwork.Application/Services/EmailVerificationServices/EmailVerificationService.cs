using SocialNetwork.Application.Services.EmailServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.EmailVerifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.EmailVerificationServices
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
            var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();


            var exist = await _emailVerificationRepository.GetByEmailAsync(email);
            if (exist != null)
            {
                var secondsSinceLastSent = (DateTime.UtcNow - exist.ExpiredAt.AddMinutes(-5)).TotalSeconds;
                if (secondsSinceLastSent < 60)
                {
                    throw new BadRequestException($"Please wait {60 - (int)secondsSinceLastSent} seconds before requesting another code.");
                }
                exist.Code = code;
                exist.ExpiredAt = DateTime.UtcNow.AddMinutes(5);

                await _emailVerificationRepository.UpdateEmailVerificationAsync(exist);
            }
            else
            {
                var verification = new EmailVerification
                {
                    Email = email,
                    Code = code,
                    ExpiredAt = DateTime.UtcNow.AddMinutes(5)
                };

                await _emailVerificationRepository.AddEmailVerificationAsync(verification);
            }

            string body = $@"
<html>
<head>
<style>
  body {{
      font-family: Arial, sans-serif;
      background-color: #f4f4f4;
      margin: 0;
      padding: 0;
  }}
  .container {{
      max-width: 600px;
      margin: 50px auto;
      background-color: #ffffff;
      padding: 30px;
      border-radius: 8px;
      box-shadow: 0 0 10px rgba(0,0,0,0.1);
  }}
  .header {{
      text-align: center;
      font-size: 24px;
      font-weight: bold;
      color: #333333;
  }}
  .content {{
      margin-top: 20px;
      font-size: 16px;
      color: #555555;
  }}
  .otp {{
      display: block;
      margin: 20px auto;
      font-size: 32px;
      font-weight: bold;
      color: #ffffff;
      background-color: #007bff;
      padding: 15px 0;
      width: 200px;
      text-align: center;
      border-radius: 8px;
      letter-spacing: 4px;
  }}
  .footer {{
      margin-top: 30px;
      font-size: 12px;
      color: #999999;
      text-align: center;
  }}
</style>
</head>
<body>
<div class='container'>
<div class='header'>Verify Your Email</div>
<div class='content'>
Hello,<br/><br/>
Your email verification code (OTP) is:
</div>
<div class='otp'>{code}</div>
<div class='content'>
This code is valid for <strong>5 minutes</strong>. Please do not share it with anyone.
</div>
<div class='footer'>
If you did not request this code, please ignore this email.<br/>
© 2025 MiniSocialNetwork
</div>
</div>
</body>
</html>
";

            await _emailService.SendEmailAsync(email, "MiniSocialNetwork - Email Verification", body, isHtml: true);
        }


        public async Task<bool> VerifyEmailAsync(string email, string code)
        {
            var isValid = await _emailVerificationRepository.VerifyCodeAsync(email, code);
            if(!isValid)
            {
                return false;
            }
            await _emailVerificationRepository.DeleteEmailVerificationAsync(email);
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
