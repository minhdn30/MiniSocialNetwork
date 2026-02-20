using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.AuthServices;
using SocialNetwork.Application.Services.EmailVerificationServices;
using System;
using System.Linq;
using System.Net;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthsController : ControllerBase
    {
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
        private readonly IEmailVerificationService _emailVerificationService;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public AuthsController(
            IAuthService authService,
            IEmailVerificationService emailVerificationService,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            _authService = authService;
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

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDTO registerDTO)
        {
            var result = await _authService.RegisterAsync(registerDTO);
            return Ok(result);
        }

        [HttpPost("send-email")]
        public async Task<IActionResult> SendVerificationEmail([FromBody] string email)
        {
            await _emailVerificationService.SendVerificationEmailAsync(email, GetClientIpAddress());
            return Ok(new { Message = "Verification email sent." });
        }

        [HttpPost("verify-code")]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
        {
            var result = await _emailVerificationService.VerifyEmailAsync(request.Email, request.Code);
            if (!result) return BadRequest(new { message = "Code is invalid or expired." });

            return Ok(new { message = "Email verification successful." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
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
    }
}
