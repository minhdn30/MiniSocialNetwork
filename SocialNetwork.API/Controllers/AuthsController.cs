using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.AuthServices;
using SocialNetwork.Application.Services.EmailVerificationServices;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthsController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IEmailVerificationService _emailVerificationService;
        public AuthsController(IAuthService authService, IEmailVerificationService emailVerificationService)
        {
            _authService = authService;
            _emailVerificationService = emailVerificationService;
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
            await _emailVerificationService.SendVerificationEmailAsync(email);
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
            var result = await _authService.LoginAsync(request);
            // Set refresh token in HttpOnly cookie
            Response.Cookies.Append("refreshToken", result.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, 
                SameSite = SameSiteMode.None,
                Expires = result.RefreshTokenExpiryTime
            });
            return Ok(result);
        }
        [HttpPost("login-with-google")]
        public async Task<ActionResult<LoginResponse>> LoginWithGoogle([FromBody] GoogleLoginRequest request)
        {
            var result = await _authService.LoginWithGoogleAsync(request.IdToken);
            return Ok(result);
        }
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized("No refresh token");

            var result = await _authService.RefreshTokenAsync(refreshToken);

            Response.Cookies.Append("refreshToken", result.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = false,                 // local
                SameSite = SameSiteMode.Lax,    
                Expires = result.RefreshTokenExpiryTime
            });

            return Ok(new
            {
                result.AccessToken,
                result.Fullname,
                result.Username,
                result.AvatarUrl
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var accountId = User.GetAccountId();
            if (accountId == null) return Unauthorized();
            await _authService.LogoutAsync(accountId.Value, Response);
            return Ok(new { message = "Logged out successfully." });
        }

    }
}
