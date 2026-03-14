using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Application.Services.AdminAuditLogServices;

namespace CloudM.API.Controllers.Admin
{
    [Route("api/admin/audit-logs")]
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    public class AdminAuditLogsController : ControllerBase
    {
        private readonly IAdminAuditLogService _adminAuditLogService;

        public AdminAuditLogsController(IAdminAuditLogService adminAuditLogService)
        {
            _adminAuditLogService = adminAuditLogService;
        }

        [HttpGet("recent")]
        public async Task<ActionResult<AdminAuditLogResponse>> GetRecentLogs([FromQuery] AdminAuditLogQueryRequest request)
        {
            var result = await _adminAuditLogService.GetRecentLogsAsync(request);
            return Ok(result);
        }
    }
}
