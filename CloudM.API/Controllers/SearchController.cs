using CloudM.Application.DTOs.SearchDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.AccountServices;
using CloudM.Application.Services.AccountSearchHistoryServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly IAccountSearchHistoryService _accountSearchHistoryService;

        public SearchController(
            IAccountService accountService,
            IAccountSearchHistoryService accountSearchHistoryService)
        {
            _accountService = accountService;
            _accountSearchHistoryService = accountSearchHistoryService;
        }

        [Authorize]
        [HttpGet("sidebar")]
        public async Task<IActionResult> SearchSidebarAccounts([FromQuery] SearchSidebarAccountsRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var normalizedKeyword = request.Keyword?.Trim() ?? string.Empty;
            var result = await _accountService.SearchSidebarAccountsAsync(
                currentId.Value,
                normalizedKeyword,
                request.Limit);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("sidebar/history")]
        public async Task<IActionResult> GetSidebarSearchHistory([FromQuery] SearchSidebarHistoryRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var result = await _accountSearchHistoryService.GetSidebarSearchHistoryAsync(currentId.Value, request.Limit);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("sidebar/history/{targetId:guid}")]
        public async Task<IActionResult> SaveSidebarSearchHistory([FromRoute] Guid targetId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            await _accountSearchHistoryService.SaveSidebarSearchHistoryAsync(currentId.Value, targetId);
            return NoContent();
        }

        [Authorize]
        [HttpDelete("sidebar/history/{targetId:guid}")]
        public async Task<IActionResult> DeleteSidebarSearchHistory([FromRoute] Guid targetId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            await _accountSearchHistoryService.DeleteSidebarSearchHistoryAsync(currentId.Value, targetId);
            return NoContent();
        }
    }
}
