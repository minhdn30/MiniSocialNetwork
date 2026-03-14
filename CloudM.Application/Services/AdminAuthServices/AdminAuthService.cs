using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Application.Services.AdminAuditLogServices;
using CloudM.Application.Services.AuthServices;
using CloudM.Application.Services.JwtServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.AdminAuths;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using System.Text.RegularExpressions;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.AdminAuthServices
{
    public class AdminAuthService : IAdminAuthService
    {
        private const int PasswordMinLength = 6;
        private static readonly Regex PasswordAccentRegex = new(@"[\u00C0-\u024F\u1E00-\u1EFF]", RegexOptions.Compiled);

        private readonly IAdminAuthRepository _adminAuthRepository;
        private readonly IAdminAuditLogService _adminAuditLogService;
        private readonly ILoginRateLimitService _loginRateLimitService;
        private readonly IJwtService _jwtService;
        private readonly IUnitOfWork _unitOfWork;

        public AdminAuthService(
            IAdminAuthRepository adminAuthRepository,
            IAdminAuditLogService adminAuditLogService,
            ILoginRateLimitService loginRateLimitService,
            IJwtService jwtService,
            IUnitOfWork unitOfWork)
        {
            _adminAuthRepository = adminAuthRepository;
            _adminAuditLogService = adminAuditLogService;
            _loginRateLimitService = loginRateLimitService;
            _jwtService = jwtService;
            _unitOfWork = unitOfWork;
        }

        public async Task<AdminLoginResponse> LoginAsync(AdminLoginRequest request, string? requesterIpAddress)
        {
            var normalizedEmail = NormalizeEmail(request.Email);
            var normalizedIpAddress = NormalizeIp(requesterIpAddress);
            var nowUtc = DateTime.UtcNow;

            try
            {
                await _loginRateLimitService.EnforceLoginAllowedAsync(normalizedEmail, normalizedIpAddress, nowUtc);
            }
            catch (InternalServerException)
            {
                // keep admin login available when rate-limit storage is unavailable
            }

            var account = await _adminAuthRepository.GetAdminByEmailAsync(normalizedEmail);

            if (account == null || string.IsNullOrWhiteSpace(account.PasswordHash))
            {
                await RecordLoginFailureAsync(normalizedEmail, normalizedIpAddress, nowUtc);
                throw new UnauthorizedException("Invalid email or password.");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
            {
                await RecordLoginFailureAsync(normalizedEmail, normalizedIpAddress, nowUtc);
                throw new UnauthorizedException("Invalid email or password.");
            }

            try
            {
                await _loginRateLimitService.ClearFailedAttemptsAsync(normalizedEmail, normalizedIpAddress, nowUtc);
            }
            catch (InternalServerException)
            {
                // best-effort only
            }

            EnsureAdminAccountAllowed(account);
            await _adminAuditLogService.RecordLoginAsync(account.AccountId, normalizedIpAddress);
            await _unitOfWork.CommitAsync();

            return BuildLoginResponse(account);
        }

        public async Task<AdminSessionResponse> GetSessionAsync(Guid accountId)
        {
            var account = await _adminAuthRepository.GetAdminByIdAsync(accountId);
            if (account == null)
            {
                throw new ForbiddenException("Admin access is not available for this account.");
            }

            EnsureAdminAccountAllowed(account);

            return new AdminSessionResponse
            {
                AccountId = account.AccountId,
                Email = account.Email,
                Fullname = account.FullName,
                Username = account.Username,
                AvatarUrl = account.AvatarUrl,
                Role = RoleEnum.Admin.ToString(),
            };
        }

        public async Task<AdminChangePasswordResponse> ChangePasswordAsync(
            Guid accountId,
            AdminChangePasswordRequest request,
            string? requesterIpAddress)
        {
            ValidatePasswordInputOrThrow(request);

            var account = await _adminAuthRepository.GetTrackedAdminByIdAsync(accountId);
            if (account == null)
            {
                throw new ForbiddenException("Admin access is not available for this account.");
            }

            EnsureAdminAccountAllowed(account);

            if (string.IsNullOrWhiteSpace(account.PasswordHash))
            {
                throw new BadRequestException("Admin password sign-in is not configured.");
            }

            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                throw new BadRequestException("Current password is required.");
            }

            var isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, account.PasswordHash);
            if (!isCurrentPasswordValid)
            {
                throw new BadRequestException("Current password is incorrect.");
            }

            var nowUtc = DateTime.UtcNow;
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            account.RefreshToken = null;
            account.RefreshTokenExpiryTime = null;
            account.UpdatedAt = nowUtc;

            await _adminAuthRepository.UpdateAsync(account);
            await _adminAuditLogService.RecordAsync(new AdminAuditLogWriteRequest
            {
                AdminId = account.AccountId,
                Module = "auth",
                ActionType = "AdminPasswordChanged",
                TargetType = "Account",
                TargetId = account.AccountId.ToString(),
                Summary = "Admin updated the password for the current admin account",
                RequestIp = NormalizeIp(requesterIpAddress),
            });
            await _unitOfWork.CommitAsync();

            return new AdminChangePasswordResponse
            {
                ChangedAt = nowUtc,
            };
        }

        private AdminLoginResponse BuildLoginResponse(Account account)
        {
            return new AdminLoginResponse
            {
                AccountId = account.AccountId,
                Email = account.Email,
                Fullname = account.FullName,
                Username = account.Username,
                AvatarUrl = account.AvatarUrl,
                Role = RoleEnum.Admin.ToString(),
                AccessToken = _jwtService.GenerateToken(account),
            };
        }

        private static void EnsureAdminAccountAllowed(Account account)
        {
            if (account.RoleId != (int)RoleEnum.Admin)
            {
                throw new ForbiddenException("Admin access is not available for this account.");
            }

            if (account.Status == AccountStatusEnum.Inactive ||
                account.Status == AccountStatusEnum.Banned ||
                account.Status == AccountStatusEnum.Suspended ||
                account.Status == AccountStatusEnum.Deleted)
            {
                throw new ForbiddenException("This admin account is currently restricted.");
            }

            if (account.Status == AccountStatusEnum.EmailNotVerified)
            {
                throw new ForbiddenException("This admin account has not verified email yet.");
            }
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string? NormalizeIp(string? requesterIpAddress)
        {
            var normalizedIp = (requesterIpAddress ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(normalizedIp) ? null : normalizedIp;
        }

        private static void ValidatePasswordInputOrThrow(AdminChangePasswordRequest request)
        {
            if (request == null)
            {
                throw new BadRequestException("Request is required.");
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                throw new BadRequestException("New password is required.");
            }

            if (request.NewPassword.Length < PasswordMinLength)
            {
                throw new BadRequestException($"Password must be at least {PasswordMinLength} characters long.");
            }

            if (request.NewPassword.Contains(' '))
            {
                throw new BadRequestException("Password cannot contain spaces.");
            }

            if (PasswordAccentRegex.IsMatch(request.NewPassword))
            {
                throw new BadRequestException("Password cannot contain Vietnamese accents.");
            }

            if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            {
                throw new BadRequestException("Password and Confirm Password do not match.");
            }
        }

        private async Task RecordLoginFailureAsync(string normalizedEmail, string? normalizedIpAddress, DateTime nowUtc)
        {
            try
            {
                await _loginRateLimitService.RecordFailedAttemptAsync(normalizedEmail, normalizedIpAddress, nowUtc);
            }
            catch (InternalServerException)
            {
                // best-effort only
            }
        }
    }
}
