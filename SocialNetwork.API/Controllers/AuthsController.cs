using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.Interfaces;

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
            return Ok(result);
        }
        [HttpPost("login-with-google")]
        public async Task<ActionResult<LoginResponse>> LoginWithGoogle([FromBody] GoogleLoginRequest request)
        {
            var result = await _authService.LoginWithGoogleAsync(request.IdToken);
            return Ok(result);
        }
    }
}
