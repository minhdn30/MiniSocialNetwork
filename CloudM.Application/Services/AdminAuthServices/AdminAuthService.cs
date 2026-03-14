using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Application.Services.AdminAuditLogServices;
using CloudM.Application.Services.AuthServices;
using CloudM.Application.Services.JwtServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.AdminAuths;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.AdminAuthServices
{
    public class AdminAuthService : IAdminAuthService
    {
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
