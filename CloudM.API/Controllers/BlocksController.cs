using CloudM.Application.DTOs.BlockDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.BlockServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BlocksController : ControllerBase
    {
        private readonly IBlockService _blockService;

        public BlocksController(IBlockService blockService)
        {
            _blockService = blockService;
        }

        [HttpGet]
        public async Task<IActionResult> GetBlockedAccounts([FromQuery] BlockedAccountListRequest request, CancellationToken cancellationToken)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized();
            }

            var result = await _blockService.GetBlockedAccountsAsync(currentId.Value, request, cancellationToken);
            return Ok(result);
        }

        [HttpGet("status/{targetId:guid}")]
        public async Task<IActionResult> GetStatus([FromRoute] Guid targetId, CancellationToken cancellationToken)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized();
            }

            var result = await _blockService.GetStatusAsync(currentId.Value, targetId, cancellationToken);
            return Ok(result);
        }

        [HttpPost("{targetId:guid}")]
        public async Task<IActionResult> Block([FromRoute] Guid targetId, CancellationToken cancellationToken)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized();
            }

            var result = await _blockService.BlockAsync(currentId.Value, targetId, cancellationToken);
            return Ok(result);
        }

        [HttpDelete("{targetId:guid}")]
        public async Task<IActionResult> Unblock([FromRoute] Guid targetId, CancellationToken cancellationToken)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized();
            }

            var result = await _blockService.UnblockAsync(currentId.Value, targetId, cancellationToken);
            return Ok(result);
        }
    }
}
