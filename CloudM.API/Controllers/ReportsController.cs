using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.ReportDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.ReportServices;

namespace CloudM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<ReportCreateResponse>> CreateReport([FromBody] ReportCreateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var result = await _reportService.CreateReportAsync(
                currentId.Value,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(result);
        }
    }
}
