using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.AdminModerationServices;

namespace CloudM.API.Controllers.Admin
{
    [Route("api/admin/moderation")]
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    public class AdminModerationController : ControllerBase
    {
        private readonly IAdminModerationService _adminModerationService;

        public AdminModerationController(IAdminModerationService adminModerationService)
        {
            _adminModerationService = adminModerationService;
        }

        [HttpGet("lookup")]
        public async Task<ActionResult<AdminModerationLookupResponse>> Lookup([FromQuery] AdminModerationLookupRequest request)
        {
            var result = await _adminModerationService.LookupAsync(request);
            return Ok(result);
        }

        [HttpPut("{targetType}/{targetId}/action")]
        public async Task<ActionResult<AdminModerationActionResponse>> ApplyAction(
            [FromRoute] string targetType,
            [FromRoute] Guid targetId,
            [FromBody] AdminModerationActionRequest request)
        {
            var adminId = User.GetAccountId();
            if (adminId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var result = await _adminModerationService.ApplyActionAsync(
                adminId.Value,
                targetId,
                request,
                targetType,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(result);
        }
    }
}
