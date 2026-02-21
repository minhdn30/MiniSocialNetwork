using Microsoft.Extensions.Options;
using SocialNetwork.Application.Services.EmailVerificationServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.EmailVerifications;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Services.Email;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.AuthServices
{
    public class PasswordResetService : IPasswordResetService
    {
        private const int OtpDigits = 6;
        private const int OtpSaltBytes = 16;
        private const int LegacyOtpHashBytes = 32;
        private const int LegacyPbkdf2Iterations = 100000;
        private const int PasswordMinLength = 6;

        private readonly IEmailService _emailService;
        private readonly IAccountRepository _accountRepository;
        private readonly IEmailVerificationRepository _emailVerificationRepository;
        private readonly IEmailVerificationRateLimitService _rateLimitService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly EmailVerificationSecurityOptions _securityOptions;

        public PasswordResetService(
            IEmailService emailService,
            IAccountRepository accountRepository,
            IEmailVerificationRepository emailVerificationRepository,
            IEmailVerificationRateLimitService rateLimitService,
            IUnitOfWork unitOfWork,
            IOptions<EmailVerificationSecurityOptions> securityOptions)
        {
            _emailService = emailService;
            _accountRepository = accountRepository;
            _emailVerificationRepository = emailVerificationRepository;
            _rateLimitService = rateLimitService;
            _unitOfWork = unitOfWork;
            _securityOptions = NormalizeOptions(securityOptions.Value);
        }

        public async Task SendResetPasswordCodeAsync(string email, string? requesterIpAddress = null)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedIpAddress = NormalizeIp(requesterIpAddress);
            var nowUtc = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                throw new BadRequestException("Email is required.");
            }

            var account = await _accountRepository.GetAccountByEmail(normalizedEmail);
            if (account == null || account.Status == AccountStatusEnum.Deleted)
            {
                // Prevent account enumeration.
                return;
            }

            await _emailVerificationRepository.EnsureExistsByEmailAsync(normalizedEmail, nowUtc);

            var verification = await _emailVerificationRepository.GetLatestByEmailAsync(normalizedEmail)
                ?? throw new BadRequestException("Unable to prepare reset request. Please try again.");

            if (verification.LockedUntil.HasValue && verification.LockedUntil.Value > nowUtc)
            {
                throw new BadRequestException(
                    $"Too many invalid attempts. Please wait {GetRemainingSeconds(verification.LockedUntil.Value, nowUtc)} seconds before requesting another code.");
            }

            ResetSendCountersIfNeeded(verification, nowUtc);

            try
            {
                await _rateLimitService.EnforceSendRateLimitAsync(normalizedEmail, normalizedIpAddress, nowUtc);
            }
            catch (InternalServerException)
            {
                EnforceSendRateLimitFallback(verification, nowUtc);
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

            var body = $@"
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
      background-color: #ff416c;
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
<div class='header'>Reset Your Password</div>
<div class='content'>
Hello,<br/><br/>
Your password reset code is:
</div>
<div class='otp'>{code}</div>
<div class='content'>
This code is valid for <strong>{_securityOptions.OtpExpiresMinutes} minutes</strong>. If you did not request a password reset, please ignore this email.
</div>
<div class='footer'>
© 2026 MiniSocialNetwork
</div>
</div>
</body>
</html>
";

            await _emailService.SendEmailAsync(normalizedEmail, "CloudM - Password Reset", body, isHtml: true);
            await _unitOfWork.CommitAsync();
        }

        public async Task<bool> VerifyResetPasswordCodeAsync(string email, string code)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedCode = NormalizeCode(code);
            var nowUtc = DateTime.UtcNow;

            if (!IsValidOtpFormat(normalizedCode))
            {
                return false;
            }

            var account = await _accountRepository.GetAccountByEmail(normalizedEmail);
            if (account == null || account.Status == AccountStatusEnum.Deleted)
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

            if (VerifyCodeHash(normalizedCode, verification.CodeHash, verification.CodeSalt))
            {
                return true;
            }

            await HandleInvalidCodeAttemptAsync(verification, nowUtc);
            return false;
        }

        public async Task ResetPasswordAsync(string email, string code, string newPassword, string confirmPassword)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedCode = NormalizeCode(code);
            var normalizedNewPassword = NormalizePassword(newPassword);
            var normalizedConfirmPassword = NormalizePassword(confirmPassword);
            var nowUtc = DateTime.UtcNow;

            ValidatePassword(normalizedNewPassword, normalizedConfirmPassword);

            if (!IsValidOtpFormat(normalizedCode))
            {
                throw new BadRequestException("Invalid or expired reset code.");
            }

            var account = await _accountRepository.GetAccountByEmail(normalizedEmail);
            if (account == null || account.Status == AccountStatusEnum.Deleted)
            {
                throw new BadRequestException("Invalid or expired reset code.");
            }

            var verification = await _emailVerificationRepository.GetLatestByEmailAsync(normalizedEmail);
            if (verification == null)
            {
                throw new BadRequestException("Invalid or expired reset code.");
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
                throw new BadRequestException("Invalid or expired reset code.");
            }

            if (!VerifyCodeHash(normalizedCode, verification.CodeHash, verification.CodeSalt))
            {
                await HandleInvalidCodeAttemptAsync(verification, nowUtc);
                throw new BadRequestException("Invalid or expired reset code.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var claimed = await _emailVerificationRepository.TryMarkAsConsumedAsync(verification.Id, nowUtc);
                if (!claimed)
                {
                    throw new BadRequestException("Reset code has already been used. Please request a new code.");
                }

                account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(normalizedNewPassword);
                account.UpdatedAt = nowUtc;
                account.RefreshToken = null;
                account.RefreshTokenExpiryTime = null;
                await _accountRepository.UpdateAccount(account);

                await _emailVerificationRepository.DeleteEmailVerificationAsync(normalizedEmail);
                return true;
            });
        }

        private async Task HandleInvalidCodeAttemptAsync(EmailVerification verification, DateTime nowUtc)
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
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeCode(string code)
        {
            return (code ?? string.Empty).Trim();
        }

        private static string NormalizePassword(string password)
        {
            return password ?? string.Empty;
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
            var hashBytes = ComputeHmacHash(code, saltBytes);
            return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
        }

        private bool VerifyCodeHash(string code, string expectedHash, string salt)
        {
            try
            {
                var saltBytes = Convert.FromBase64String(salt);
                var computedHashBytes = ComputeHmacHash(code, saltBytes);
                var expectedHashBytes = Convert.FromBase64String(expectedHash);

                if (CryptographicOperations.FixedTimeEquals(computedHashBytes, expectedHashBytes))
                {
                    return true;
                }

                var legacyPepperedCode = $"{code}:{_securityOptions.OtpPepper}";
                var legacyHashBytes = Rfc2898DeriveBytes.Pbkdf2(
                    legacyPepperedCode,
                    saltBytes,
                    LegacyPbkdf2Iterations,
                    HashAlgorithmName.SHA256,
                    LegacyOtpHashBytes);

                return CryptographicOperations.FixedTimeEquals(legacyHashBytes, expectedHashBytes);
            }
            catch
            {
                return false;
            }
        }

        private byte[] ComputeHmacHash(string code, byte[] saltBytes)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_securityOptions.OtpPepper));
            var payload = BuildOtpPayload(code, saltBytes);
            return hmac.ComputeHash(payload);
        }

        private static byte[] BuildOtpPayload(string code, byte[] saltBytes)
        {
            var codeBytes = Encoding.UTF8.GetBytes(code);
            var payload = new byte[saltBytes.Length + 1 + codeBytes.Length];
            Buffer.BlockCopy(saltBytes, 0, payload, 0, saltBytes.Length);
            payload[saltBytes.Length] = (byte)':';
            Buffer.BlockCopy(codeBytes, 0, payload, saltBytes.Length + 1, codeBytes.Length);
            return payload;
        }

        private static int GetRemainingSeconds(DateTime futureTime, DateTime nowUtc)
        {
            return Math.Max(1, (int)Math.Ceiling((futureTime - nowUtc).TotalSeconds));
        }

        private void EnforceSendRateLimitFallback(EmailVerification verification, DateTime nowUtc)
        {
            if (verification.LastSentAt > nowUtc.AddSeconds(-_securityOptions.ResendCooldownSeconds))
            {
                throw new BadRequestException(
                    $"Please wait {GetRemainingSeconds(verification.LastSentAt.AddSeconds(_securityOptions.ResendCooldownSeconds), nowUtc)} seconds before requesting another code.");
            }

            if (verification.SendCountInWindow >= _securityOptions.MaxSendsPerWindow)
            {
                throw new BadRequestException(
                    $"You have reached the OTP request limit for this email. Please wait {GetRemainingSeconds(verification.SendWindowStartedAt.AddMinutes(_securityOptions.SendWindowMinutes), nowUtc)} seconds.");
            }

            if (verification.DailySendCount >= _securityOptions.MaxSendsPerDay)
            {
                throw new BadRequestException("You have reached today's OTP request limit for this email.");
            }
        }

        private void ResetSendCountersIfNeeded(EmailVerification verification, DateTime nowUtc)
        {
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
        }

        private static EmailVerificationSecurityOptions NormalizeOptions(EmailVerificationSecurityOptions options)
        {
            options.OtpExpiresMinutes = options.OtpExpiresMinutes <= 0 ? 5 : options.OtpExpiresMinutes;
            options.ResendCooldownSeconds = options.ResendCooldownSeconds <= 0 ? 60 : options.ResendCooldownSeconds;
            options.MaxSendsPerWindow = options.MaxSendsPerWindow <= 0 ? 3 : options.MaxSendsPerWindow;
            options.SendWindowMinutes = options.SendWindowMinutes <= 0 ? 15 : options.SendWindowMinutes;
            options.MaxSendsPerDay = options.MaxSendsPerDay <= 0 ? 10 : options.MaxSendsPerDay;
            options.MaxFailedAttempts = options.MaxFailedAttempts <= 0 ? 5 : options.MaxFailedAttempts;
            options.LockMinutes = options.LockMinutes <= 0 ? 15 : options.LockMinutes;
            options.OtpPepper = string.IsNullOrWhiteSpace(options.OtpPepper)
                ? "CHANGE_ME_OTP_PEPPER"
                : options.OtpPepper;
            return options;
        }

        private static void ValidatePassword(string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                throw new BadRequestException("New password is required.");
            }

            if (newPassword.Length < PasswordMinLength)
            {
                throw new BadRequestException($"Password must be at least {PasswordMinLength} characters long.");
            }

            if (newPassword.Contains(' '))
            {
                throw new BadRequestException("Password cannot contain spaces.");
            }

            if (Regex.IsMatch(newPassword, @"[\u00C0-\u024F\u1E00-\u1EFF]"))
            {
                throw new BadRequestException("Password cannot contain Vietnamese accents.");
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                throw new BadRequestException("Password and Confirm Password do not match.");
            }
        }
    }
}
