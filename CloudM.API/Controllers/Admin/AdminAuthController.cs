using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.AdminAuthServices;

namespace CloudM.API.Controllers.Admin
{
    [Route("api/admin/auth")]
    [ApiController]
    public class AdminAuthController : ControllerBase
    {
        private readonly IAdminAuthService _adminAuthService;

        public AdminAuthController(IAdminAuthService adminAuthService)
        {
            _adminAuthService = adminAuthService;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AdminLoginResponse>> Login([FromBody] AdminLoginRequest request)
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

            var requesterIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _adminAuthService.LoginAsync(request, requesterIpAddress);
            return Ok(result);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpGet("session")]
        public async Task<ActionResult<AdminSessionResponse>> GetSession()
        {
            var accountId = User.GetAccountId();
            if (accountId == null)
            {
                return Unauthorized();
            }

            var result = await _adminAuthService.GetSessionAsync(accountId.Value);
            return Ok(result);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("change-password")]
        public async Task<ActionResult<AdminChangePasswordResponse>> ChangePassword([FromBody] AdminChangePasswordRequest request)
        {
            var accountId = User.GetAccountId();
            if (accountId == null)
            {
                return Unauthorized();
            }

            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            var requesterIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _adminAuthService.ChangePasswordAsync(accountId.Value, request, requesterIpAddress);
            return Ok(result);
        }
    }
}
