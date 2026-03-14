using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.AdminReportServices;

namespace CloudM.API.Controllers.Admin
{
    [Route("api/admin/reports")]
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    public class AdminReportsController : ControllerBase
    {
        private readonly IAdminReportService _adminReportService;

        public AdminReportsController(IAdminReportService adminReportService)
        {
            _adminReportService = adminReportService;
        }

        [HttpGet("recent")]
        public async Task<ActionResult<AdminReportListResponse>> GetRecent([FromQuery] AdminReportListRequest request)
        {
            var result = await _adminReportService.GetRecentReportsAsync(request);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<AdminReportItemResponse>> CreateInternalReport([FromBody] AdminReportCreateRequest request)
        {
            var adminId = User.GetAccountId();
            if (adminId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var result = await _adminReportService.CreateInternalReportAsync(
                adminId.Value,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(result);
        }

        [HttpPut("{moderationReportId}/status")]
        public async Task<ActionResult<AdminReportItemResponse>> UpdateStatus(
            [FromRoute] Guid moderationReportId,
            [FromBody] AdminReportStatusUpdateRequest request)
        {
            var adminId = User.GetAccountId();
            if (adminId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var result = await _adminReportService.UpdateStatusAsync(
                adminId.Value,
                moderationReportId,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(result);
        }
    }
}
