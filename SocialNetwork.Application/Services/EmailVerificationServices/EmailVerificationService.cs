using Microsoft.Extensions.Options;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.EmailVerificationIpRateLimits;
using SocialNetwork.Infrastructure.Repositories.EmailVerifications;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Services.Email;
using System.Net;
using System.Security.Cryptography;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.EmailVerificationServices
{
    public class EmailVerificationService : IEmailVerificationService
    {
        private const int OtpDigits = 6;
        private const int OtpSaltBytes = 16;
        private const int OtpHashBytes = 32;

        private readonly IEmailService _emailService;
        private readonly IAccountRepository _accountRepository;
        private readonly IEmailVerificationRepository _emailVerificationRepository;
        private readonly IEmailVerificationIpRateLimitRepository _ipRateLimitRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly EmailVerificationSecurityOptions _securityOptions;

        public EmailVerificationService(
            IEmailService emailService,
            IEmailVerificationRepository emailVerificationRepository,
            IEmailVerificationIpRateLimitRepository ipRateLimitRepository,
            IAccountRepository accountRepository,
            IUnitOfWork unitOfWork,
            IOptions<EmailVerificationSecurityOptions> securityOptions)
        {
            _emailService = emailService;
            _emailVerificationRepository = emailVerificationRepository;
            _ipRateLimitRepository = ipRateLimitRepository;
            _accountRepository = accountRepository;
            _unitOfWork = unitOfWork;
            _securityOptions = NormalizeOptions(securityOptions.Value);
        }

        public async Task SendVerificationEmailAsync(string email, string? requesterIpAddress = null)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedIpAddress = NormalizeIp(requesterIpAddress);
            var nowUtc = DateTime.UtcNow;

            var account = await _accountRepository.GetAccountByEmail(normalizedEmail);
            if (account == null || account.Status != AccountStatusEnum.EmailNotVerified)
            {
                throw new BadRequestException("Unable to send verification code for this email.");
            }

            await _emailVerificationRepository.EnsureExistsByEmailAsync(normalizedEmail, nowUtc);

            EmailVerificationIpRateLimit? ipRateLimit = null;
            if (!string.IsNullOrWhiteSpace(normalizedIpAddress))
            {
                await _ipRateLimitRepository.EnsureExistsByIpAsync(normalizedIpAddress, nowUtc);
                ipRateLimit = await _ipRateLimitRepository.GetByIpAsync(normalizedIpAddress);
            }

            await EnforceIpRateLimitBeforeSendAsync(ipRateLimit, nowUtc);

            var verification = await _emailVerificationRepository.GetLatestByEmailAsync(normalizedEmail)
                ?? throw new BadRequestException("Unable to prepare verification request. Please try again.");

            if (verification.LockedUntil.HasValue && verification.LockedUntil.Value > nowUtc)
            {
                throw new BadRequestException(
                    $"Too many invalid attempts. Please wait {GetRemainingSeconds(verification.LockedUntil.Value, nowUtc)} seconds before requesting another code.");
            }

            if (verification.SendWindowStartedAt <= nowUtc.AddMinutes(-_securityOptions.SendWindowMinutes))
            {
                verification.SendWindowStartedAt = nowUtc;
                verification.SendCountInWindow = 0;
            }

            if (verification.DailyWindowStartedAt.Date != nowUtc.Date)
            {
                verification.DailyWindowStartedAt = nowUtc.Date;
                verification.DailySendCount = 0;
            }

            if (verification.LastSentAt > nowUtc.AddSeconds(-_securityOptions.ResendCooldownSeconds))
            {
                throw new BadRequestException(
                    $"Please wait {GetRemainingSeconds(verification.LastSentAt.AddSeconds(_securityOptions.ResendCooldownSeconds), nowUtc)} seconds before requesting another code.");
            }

            if (verification.SendCountInWindow >= _securityOptions.MaxSendsPerWindow)
            {
                throw new BadRequestException(
                    $"You have reached the OTP request limit. Please wait {GetRemainingSeconds(verification.SendWindowStartedAt.AddMinutes(_securityOptions.SendWindowMinutes), nowUtc)} seconds.");
            }

            if (verification.DailySendCount >= _securityOptions.MaxSendsPerDay)
            {
                throw new BadRequestException("You have reached today's OTP request limit. Please try again tomorrow.");
            }

            var code = GenerateOtpCode();
            var (hash, salt) = HashCode(code);

            verification.CodeHash = hash;
            verification.CodeSalt = salt;
            verification.ExpiredAt = nowUtc.AddMinutes(_securityOptions.OtpExpiresMinutes);
            verification.CreatedAt = nowUtc;
            verification.LastSentAt = nowUtc;
            verification.SendCountInWindow += 1;
            verification.DailySendCount += 1;
            verification.FailedAttempts = 0;
            verification.LockedUntil = null;
            verification.ConsumedAt = null;

            if (ipRateLimit != null)
            {
                ipRateLimit.LastSentAt = nowUtc;
                ipRateLimit.SendCountInWindow += 1;
                ipRateLimit.DailySendCount += 1;
                ipRateLimit.LockedUntil = null;
                ipRateLimit.UpdatedAt = nowUtc;

                if (ipRateLimit.Id > 0)
                {
                    await _ipRateLimitRepository.UpdateAsync(ipRateLimit);
                }
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
This code is valid for <strong>{_securityOptions.OtpExpiresMinutes} minutes</strong>. Please do not share it with anyone.
</div>
<div class='footer'>
If you did not request this code, please ignore this email.<br/>
© 2025 MiniSocialNetwork
</div>
</div>
</body>
</html>
";

            await _emailService.SendEmailAsync(normalizedEmail, "CloudM - Email Verification", body, isHtml: true);
            await _unitOfWork.CommitAsync();
        }


        public async Task<bool> VerifyEmailAsync(string email, string code)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedCode = NormalizeCode(code);
            var nowUtc = DateTime.UtcNow;

            if (!IsValidOtpFormat(normalizedCode))
            {
                return false;
            }

            var verification = await _emailVerificationRepository.GetLatestByEmailAsync(normalizedEmail);
            if (verification == null)
            {
                return false;
            }

            if (verification.LockedUntil.HasValue && verification.LockedUntil.Value > nowUtc)
            {
                throw new BadRequestException(
                    $"Too many invalid attempts. Please wait {GetRemainingSeconds(verification.LockedUntil.Value, nowUtc)} seconds before trying again.");
            }

            if (verification.ExpiredAt <= nowUtc)
            {
                await _emailVerificationRepository.DeleteEmailVerificationAsync(normalizedEmail);
                await _unitOfWork.CommitAsync();
                return false;
            }

            if (!VerifyCodeHash(normalizedCode, verification.CodeHash, verification.CodeSalt))
            {
                verification.FailedAttempts += 1;

                if (verification.FailedAttempts >= _securityOptions.MaxFailedAttempts)
                {
                    verification.LockedUntil = nowUtc.AddMinutes(_securityOptions.LockMinutes);
                    await _emailVerificationRepository.UpdateEmailVerificationAsync(verification);
                    await _unitOfWork.CommitAsync();

                    throw new BadRequestException(
                        $"Too many invalid attempts. Please wait {GetRemainingSeconds(verification.LockedUntil.Value, nowUtc)} seconds before trying again.");
                }

                await _emailVerificationRepository.UpdateEmailVerificationAsync(verification);
                await _unitOfWork.CommitAsync();
                return false;
            }

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var claimed = await _emailVerificationRepository.TryMarkAsConsumedAsync(verification.Id, nowUtc);
                if (!claimed)
                {
                    throw new BadRequestException("Verification code has already been used. Please request a new code.");
                }

                var account = await _accountRepository.GetAccountByEmail(normalizedEmail);
                if (account != null && account.Status == AccountStatusEnum.EmailNotVerified)
                {
                    account.Status = AccountStatusEnum.Active;
                    account.UpdatedAt = nowUtc;
                    await _accountRepository.UpdateAccount(account);
                }

                await _emailVerificationRepository.DeleteEmailVerificationAsync(normalizedEmail);
                return account != null;
            });
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeCode(string code)
        {
            return (code ?? string.Empty).Trim();
        }

        private static string? NormalizeIp(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return null;
            }

            var candidate = ipAddress.Split(',')[0].Trim();
            if (!IPAddress.TryParse(candidate, out var parsedIp))
            {
                return null;
            }

            if (parsedIp.IsIPv4MappedToIPv6)
            {
                parsedIp = parsedIp.MapToIPv4();
            }

            return parsedIp.ToString();
        }

        private static bool IsValidOtpFormat(string code)
        {
            return code.Length == OtpDigits && code.All(char.IsDigit);
        }

        private string GenerateOtpCode()
        {
            return RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        }

        private (string Hash, string Salt) HashCode(string code)
        {
            var saltBytes = RandomNumberGenerator.GetBytes(OtpSaltBytes);
            var pepperedCode = $"{code}:{_securityOptions.OtpPepper}";
            var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
                pepperedCode,
                saltBytes,
                _securityOptions.Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                OtpHashBytes);

            return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
        }

        private bool VerifyCodeHash(string code, string expectedHash, string salt)
        {
            try
            {
                var saltBytes = Convert.FromBase64String(salt);
                var pepperedCode = $"{code}:{_securityOptions.OtpPepper}";
                var computedHashBytes = Rfc2898DeriveBytes.Pbkdf2(
                    pepperedCode,
                    saltBytes,
                    _securityOptions.Pbkdf2Iterations,
                    HashAlgorithmName.SHA256,
                    OtpHashBytes);
                var expectedHashBytes = Convert.FromBase64String(expectedHash);

                return CryptographicOperations.FixedTimeEquals(computedHashBytes, expectedHashBytes);
            }
            catch
            {
                return false;
            }
        }

        private static int GetRemainingSeconds(DateTime futureTime, DateTime nowUtc)
        {
            return Math.Max(1, (int)Math.Ceiling((futureTime - nowUtc).TotalSeconds));
        }

        private async Task EnforceIpRateLimitBeforeSendAsync(EmailVerificationIpRateLimit? ipRateLimit, DateTime nowUtc)
        {
            if (ipRateLimit == null)
            {
                return;
            }

            if (ipRateLimit.LockedUntil.HasValue && ipRateLimit.LockedUntil.Value > nowUtc)
            {
                throw new BadRequestException(
                    $"Too many OTP requests from this network. Please wait {GetRemainingSeconds(ipRateLimit.LockedUntil.Value, nowUtc)} seconds before requesting another code.");
            }

            if (ipRateLimit.SendWindowStartedAt <= nowUtc.AddMinutes(-_securityOptions.IpSendWindowMinutes))
            {
                ipRateLimit.SendWindowStartedAt = nowUtc;
                ipRateLimit.SendCountInWindow = 0;
            }

            if (ipRateLimit.DailyWindowStartedAt.Date != nowUtc.Date)
            {
                ipRateLimit.DailyWindowStartedAt = nowUtc.Date;
                ipRateLimit.DailySendCount = 0;
            }

            if (ipRateLimit.DailySendCount >= _securityOptions.MaxSendsPerIpDay)
            {
                var nextDayUtc = nowUtc.Date.AddDays(1);
                ipRateLimit.LockedUntil = nextDayUtc;
                ipRateLimit.UpdatedAt = nowUtc;

                if (ipRateLimit.Id > 0)
                {
                    await _ipRateLimitRepository.UpdateAsync(ipRateLimit);
                }

                await _unitOfWork.CommitAsync();
                throw new BadRequestException(
                    $"This network has reached today's OTP request limit. Please wait {GetRemainingSeconds(nextDayUtc, nowUtc)} seconds.");
            }

            if (ipRateLimit.SendCountInWindow >= _securityOptions.MaxSendsPerIpWindow)
            {
                var lockedUntil = nowUtc.AddMinutes(_securityOptions.IpLockMinutes);
                ipRateLimit.LockedUntil = lockedUntil;
                ipRateLimit.UpdatedAt = nowUtc;

                if (ipRateLimit.Id > 0)
                {
                    await _ipRateLimitRepository.UpdateAsync(ipRateLimit);
                }

                await _unitOfWork.CommitAsync();
                throw new BadRequestException(
                    $"Too many OTP requests from this network. Please wait {GetRemainingSeconds(lockedUntil, nowUtc)} seconds before requesting another code.");
            }
        }

        private static EmailVerificationSecurityOptions NormalizeOptions(EmailVerificationSecurityOptions options)
        {
            options.OtpExpiresMinutes = options.OtpExpiresMinutes <= 0 ? 5 : options.OtpExpiresMinutes;
            options.ResendCooldownSeconds = options.ResendCooldownSeconds <= 0 ? 60 : options.ResendCooldownSeconds;
            options.MaxSendsPerWindow = options.MaxSendsPerWindow <= 0 ? 3 : options.MaxSendsPerWindow;
            options.SendWindowMinutes = options.SendWindowMinutes <= 0 ? 15 : options.SendWindowMinutes;
            options.MaxSendsPerDay = options.MaxSendsPerDay <= 0 ? 10 : options.MaxSendsPerDay;
            options.MaxSendsPerIpWindow = options.MaxSendsPerIpWindow <= 0 ? 10 : options.MaxSendsPerIpWindow;
            options.IpSendWindowMinutes = options.IpSendWindowMinutes <= 0 ? 15 : options.IpSendWindowMinutes;
            options.MaxSendsPerIpDay = options.MaxSendsPerIpDay <= 0 ? 30 : options.MaxSendsPerIpDay;
            options.IpLockMinutes = options.IpLockMinutes <= 0 ? 15 : options.IpLockMinutes;
            options.MaxFailedAttempts = options.MaxFailedAttempts <= 0 ? 5 : options.MaxFailedAttempts;
            options.LockMinutes = options.LockMinutes <= 0 ? 15 : options.LockMinutes;
            options.CleanupIntervalMinutes = options.CleanupIntervalMinutes <= 0 ? 30 : options.CleanupIntervalMinutes;
            options.RetentionHours = options.RetentionHours <= 0 ? 24 : options.RetentionHours;
            options.Pbkdf2Iterations = options.Pbkdf2Iterations <= 0 ? 100000 : options.Pbkdf2Iterations;
            options.OtpPepper = string.IsNullOrWhiteSpace(options.OtpPepper)
                ? "CHANGE_ME_OTP_PEPPER"
                : options.OtpPepper;

            return options;
        }
    }
}
