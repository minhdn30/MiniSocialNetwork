using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.AdminAccountLookupServices;
using CloudM.Application.Services.AdminAccountStatusServices;

namespace CloudM.API.Controllers.Admin
{
    [Route("api/admin/accounts")]
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    public class AdminAccountsController : ControllerBase
    {
        private readonly IAdminAccountLookupService _adminAccountLookupService;
        private readonly IAdminAccountStatusService _adminAccountStatusService;

        public AdminAccountsController(
            IAdminAccountLookupService adminAccountLookupService,
            IAdminAccountStatusService adminAccountStatusService)
        {
            _adminAccountLookupService = adminAccountLookupService;
            _adminAccountStatusService = adminAccountStatusService;
        }

        [HttpGet("lookup")]
        public async Task<ActionResult<AdminAccountLookupResponse>> Lookup([FromQuery] AdminAccountLookupRequest request)
        {
            var result = await _adminAccountLookupService.LookupAccountsAsync(request);
            return Ok(result);
        }

        [HttpPut("{accountId}/status")]
        public async Task<ActionResult<AdminAccountStatusUpdateResponse>> UpdateStatus(
            [FromRoute] Guid accountId,
            [FromBody] AdminAccountStatusUpdateRequest request)
        {
            var adminId = User.GetAccountId();
            if (adminId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var result = await _adminAccountStatusService.UpdateStatusAsync(
                adminId.Value,
                accountId,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(result);
        }
    }
}
