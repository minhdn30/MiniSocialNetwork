using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.AuthServices;
using SocialNetwork.Application.Services.EmailVerificationServices;
using SocialNetwork.Domain.Enums;
using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthsController : ControllerBase
    {
        private const int PasswordMinLength = 6;
        private static readonly Regex PasswordAccentRegex = new(@"[\u00C0-\u024F\u1E00-\u1EFF]", RegexOptions.Compiled);

        private static readonly string[] DefaultAllowedOrigins = new[]
        {
            "http://127.0.0.1:5500",
            "http://localhost:5500",
            "http://127.0.0.1:5502",
            "http://localhost:5502",
            "http://127.0.0.1:5503",
            "http://localhost:5503",
            "https://127.0.0.1:5500",
            "https://localhost:5500",
            "https://127.0.0.1:5502",
            "https://localhost:5502",
            "https://127.0.0.1:5503",
            "https://localhost:5503"
        };

        private readonly IAuthService _authService;
        private readonly IPasswordResetService _passwordResetService;
        private readonly IEmailVerificationService _emailVerificationService;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public AuthsController(
            IAuthService authService,
            IPasswordResetService passwordResetService,
            IEmailVerificationService emailVerificationService,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            _authService = authService;
            _passwordResetService = passwordResetService;
            _emailVerificationService = emailVerificationService;
            _configuration = configuration;
            _environment = environment;
        }

        private string[] GetAllowedOrigins()
        {
            var configured = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
            if (configured == null || configured.Length == 0)
            {
                return DefaultAllowedOrigins;
            }
            return configured
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(origin => origin.Trim().TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private string? GetRequestOrigin()
        {
            var origin = Request.Headers["Origin"].ToString();
            if (!string.IsNullOrWhiteSpace(origin))
            {
                return origin.TrimEnd('/');
            }

            var referer = Request.Headers["Referer"].ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            {
                return $"{refererUri.Scheme}://{refererUri.Authority}".TrimEnd('/');
            }

            return null;
        }

        private bool IsTrustedBrowserOrigin()
        {
            var requestOrigin = GetRequestOrigin();
            if (string.IsNullOrWhiteSpace(requestOrigin))
            {
                // Non-browser clients or same-origin requests without Origin header.
                return true;
            }

            var normalizedRequestOrigin = requestOrigin.TrimEnd('/');
            var inAllowList = GetAllowedOrigins().Any(origin =>
                string.Equals(origin, normalizedRequestOrigin, StringComparison.OrdinalIgnoreCase));
            if (inAllowList)
            {
                return true;
            }

            return _environment.IsDevelopment() && IsLoopbackOrigin(normalizedRequestOrigin);
        }

        private static bool IsLoopbackOrigin(string origin)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return false;
            }

            var isHttpScheme =
                string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            if (!isHttpScheme)
            {
                return false;
            }

            return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult? RejectIfUntrustedOrigin()
        {
            if (IsTrustedBrowserOrigin())
            {
                return null;
            }

            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Invalid request origin." });
        }

        private CookieOptions BuildRefreshTokenCookieOptions(DateTime expiresUtc)
        {
            var secure = IsSecureRequest();
            var sameSite = secure ? SameSiteMode.None : SameSiteMode.Lax;
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = sameSite,
                Expires = expiresUtc,
                Path = "/",
                IsEssential = true
            };
        }

        private CookieOptions BuildRefreshTokenDeleteCookieOptions()
        {
            var secure = IsSecureRequest();
            var sameSite = secure ? SameSiteMode.None : SameSiteMode.Lax;
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = sameSite,
                Path = "/",
                Expires = DateTimeOffset.UnixEpoch,
                IsEssential = true
            };
        }

        private bool IsSecureRequest()
        {
            if (Request.IsHttps)
            {
                return true;
            }

            var forwardedProto = Request.Headers["X-Forwarded-Proto"].ToString();
            if (!string.IsNullOrWhiteSpace(forwardedProto))
            {
                var firstProto = forwardedProto.Split(',')[0].Trim();
                if (string.Equals(firstProto, "https", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string? GetClientIpAddress()
        {
            IPAddress? remoteIp = HttpContext.Connection.RemoteIpAddress;
            if (remoteIp == null)
            {
                return null;
            }

            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            return remoteIp.ToString();
        }

        private IActionResult? ValidatePasswordInput(string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                return BadRequest(new { message = "New password is required." });
            }

            if (newPassword.Length < PasswordMinLength)
            {
                return BadRequest(new { message = $"Password must be at least {PasswordMinLength} characters long." });
            }

            if (newPassword.Contains(' '))
            {
                return BadRequest(new { message = "Password cannot contain spaces." });
            }

            if (PasswordAccentRegex.IsMatch(newPassword))
            {
                return BadRequest(new { message = "Password cannot contain Vietnamese accents." });
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                return BadRequest(new { message = "Password and Confirm Password do not match." });
            }

            return null;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO registerDTO)
        {
            if (registerDTO == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            if (string.IsNullOrWhiteSpace(registerDTO.Username))
            {
                return BadRequest(new { message = "Username is required." });
            }

            if (string.IsNullOrWhiteSpace(registerDTO.Email))
            {
                return BadRequest(new { message = "Email is required." });
            }

            var result = await _authService.RegisterAsync(registerDTO);
            return Ok(result);
        }

        [HttpPost("send-email")]
        public async Task<IActionResult> SendVerificationEmail([FromBody] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { message = "Email is required." });
            }

            await _emailVerificationService.SendVerificationEmailAsync(email, GetClientIpAddress());
            return Ok(new { Message = "Verification email sent." });
        }

        [HttpPost("verify-code")]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { message = "Code is required." });
            }

            var result = await _emailVerificationService.VerifyEmailAsync(request.Email, request.Code);
            if (!result) return BadRequest(new { message = "Code is invalid or expired." });

            return Ok(new { message = "Email verification successful." });
        }

        [HttpPost("forgot-password/send-code")]
        public async Task<IActionResult> SendForgotPasswordCode([FromBody] ForgotPasswordRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email is required." });
            }

            await _passwordResetService.SendResetPasswordCodeAsync(request.Email, GetClientIpAddress());
            return Ok(new { message = "If the email exists, a reset code has been sent." });
        }

        [HttpPost("forgot-password/verify-code")]
        public async Task<IActionResult> VerifyForgotPasswordCode([FromBody] ForgotPasswordVerifyRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { message = "Code is required." });
            }

            var isValid = await _passwordResetService.VerifyResetPasswordCodeAsync(request.Email, request.Code);
            if (!isValid)
            {
                return BadRequest(new { message = "Code is invalid or expired." });
            }

            return Ok(new { message = "Code verified successfully." });
        }

        [HttpPost("forgot-password/reset")]
        public async Task<IActionResult> ResetForgottenPassword([FromBody] ForgotPasswordResetRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { message = "Code is required." });
            }

            var passwordValidationError = ValidatePasswordInput(request.NewPassword, request.ConfirmPassword);
            if (passwordValidationError != null)
            {
                return passwordValidationError;
            }

            await _passwordResetService.ResetPasswordAsync(
                request.Email,
                request.Code,
                request.NewPassword,
                request.ConfirmPassword);

            return Ok(new { message = "Password reset successful." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Password is required." });
            }

            var result = await _authService.LoginAsync(request, GetClientIpAddress());
            if (result == null)
                return Unauthorized(new { message = "Login failed." });

            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                Response.Cookies.Append(
                    "refreshToken",
                    result.RefreshToken,
                    BuildRefreshTokenCookieOptions(result.RefreshTokenExpiryTime));
            }

            return Ok(result);
        }

        [HttpPost("login-with-google")]
        public async Task<ActionResult<LoginResponse>> LoginWithGoogle([FromBody] GoogleLoginRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            if (string.IsNullOrWhiteSpace(request.IdToken))
            {
                return BadRequest(new { message = "Google credential is required." });
            }

            var result = await _authService.LoginWithGoogleAsync(request.IdToken);

            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                Response.Cookies.Append(
                    "refreshToken",
                    result.RefreshToken,
                    BuildRefreshTokenCookieOptions(result.RefreshTokenExpiryTime));
            }

            return Ok(result);
        }

        [HttpPost("external-login")]
        public async Task<ActionResult<LoginResponse>> LoginWithExternal([FromBody] ExternalLoginRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Provider))
            {
                return BadRequest(new { message = "Provider is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Credential))
            {
                return BadRequest(new { message = "Credential is required." });
            }

            if (!Enum.TryParse<ExternalLoginProviderEnum>(request.Provider, true, out var provider))
            {
                return BadRequest(new { message = "Unsupported provider." });
            }

            var result = await _authService.LoginWithExternalAsync(provider, request.Credential);
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                Response.Cookies.Append(
                    "refreshToken",
                    result.RefreshToken,
                    BuildRefreshTokenCookieOptions(result.RefreshTokenExpiryTime));
            }

            return Ok(result);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken()
        {
            var originError = RejectIfUntrustedOrigin();
            if (originError != null)
                return originError;

            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized("No refresh token");

            var result = await _authService.RefreshTokenAsync(refreshToken);
            if (result == null)
                return Unauthorized(new { message = "Invalid or expired refresh token." });

            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                Response.Cookies.Append(
                    "refreshToken",
                    result.RefreshToken,
                    BuildRefreshTokenCookieOptions(result.RefreshTokenExpiryTime));
            }

            return Ok(new
            {
                result.AccessToken,
                result.AccountId,
                result.Fullname,
                result.Username,
                result.AvatarUrl
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var originError = RejectIfUntrustedOrigin();
            if (originError != null)
                return originError;

            var accountId = User.GetAccountId();
            if (accountId == null) return Unauthorized();

            await _authService.LogoutAsync(accountId.Value, Response);
            Response.Cookies.Delete("refreshToken", BuildRefreshTokenDeleteCookieOptions());

            return Ok(new { message = "Logged out successfully." });
        }

        [Authorize]
        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
        {
            var accountId = User.GetAccountId();
            if (accountId == null) return Unauthorized();

            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            var passwordValidationError = ValidatePasswordInput(request.NewPassword, request.ConfirmPassword);
            if (passwordValidationError != null)
            {
                return passwordValidationError;
            }

            await _authService.SetPasswordAsync(accountId.Value, request.NewPassword, request.ConfirmPassword);
            return Ok(new { message = "Password set successfully." });
        }

        [Authorize]
        [HttpGet("external-logins")]
        public async Task<IActionResult> GetExternalLogins()
        {
            var accountId = User.GetAccountId();
            if (accountId == null) return Unauthorized();

            var result = await _authService.GetExternalLoginsAsync(accountId.Value);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("external-logins/{provider}")]
        public async Task<IActionResult> UnlinkExternalLogin([FromRoute] string provider)
        {
            var accountId = User.GetAccountId();
            if (accountId == null) return Unauthorized();

            if (!Enum.TryParse<ExternalLoginProviderEnum>(provider, true, out var parsedProvider))
            {
                return BadRequest(new { message = "Unsupported provider." });
            }

            await _authService.UnlinkExternalLoginAsync(accountId.Value, parsedProvider);
            return Ok(new { message = "External login unlinked successfully." });
        }
    }
}
