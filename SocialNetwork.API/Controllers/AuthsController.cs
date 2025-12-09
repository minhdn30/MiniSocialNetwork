using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AuthDTOs;
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
        [HttpPost("login-with-username")]
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
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            // Lấy refresh token từ cookie nếu FE không gửi body
            var token = request.RefreshToken ?? Request.Cookies["refreshToken"];
            var result = await _authService.RefreshTokenAsync(token);

            // Gửi refresh token mới qua cookie
            Response.Cookies.Append("refreshToken", result.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.None,
                Expires = result.RefreshTokenExpiryTime
            });

            return Ok(new
            {
                result.AccessToken,
                result.RefreshToken,
                result.Fullname,
                result.AvatarUrl
            });
        }
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var accountIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (accountIdClaim == null || !Guid.TryParse(accountIdClaim, out var accountId))
                return Unauthorized();

            await _authService.LogoutAsync(accountId, Response);

            return Ok(new { message = "Logged out successfully." });
        }

    }
}
