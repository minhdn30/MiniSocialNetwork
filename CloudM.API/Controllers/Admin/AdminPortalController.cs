using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Application.Services.AdminPortalServices;

namespace CloudM.API.Controllers.Admin
{
    [Route("api/admin/portal")]
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    public class AdminPortalController : ControllerBase
    {
        private readonly IAdminPortalService _adminPortalService;

        public AdminPortalController(IAdminPortalService adminPortalService)
        {
            _adminPortalService = adminPortalService;
        }

        [HttpGet("bootstrap")]
        public async Task<ActionResult<AdminPortalBootstrapResponse>> GetBootstrap()
        {
            var result = await _adminPortalService.GetBootstrapAsync();
            return Ok(result);
        }
    }
}
