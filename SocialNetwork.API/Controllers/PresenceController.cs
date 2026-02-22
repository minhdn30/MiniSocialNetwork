using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.PresenceDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.PresenceServices;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PresenceController : ControllerBase
    {
        private readonly IOnlinePresenceService _onlinePresenceService;
        private readonly OnlinePresenceOptions _onlinePresenceOptions;

        public PresenceController(
            IOnlinePresenceService onlinePresenceService,
            Microsoft.Extensions.Options.IOptions<OnlinePresenceOptions> onlinePresenceOptions)
        {
            _onlinePresenceService = onlinePresenceService;
            _onlinePresenceOptions = (onlinePresenceOptions.Value ?? new OnlinePresenceOptions()).Normalize();
        }

        [HttpPost("snapshot")]
        public async Task<ActionResult<PresenceSnapshotResponse>> Snapshot([FromBody] PresenceSnapshotRequest request, CancellationToken cancellationToken)
        {
            var viewerAccountId = User.GetAccountId();
            if (viewerAccountId == null || viewerAccountId.Value == Guid.Empty)
            {
                return Unauthorized();
            }

            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            if (request.AccountIds == null || request.AccountIds.Count == 0)
            {
                return BadRequest(new { message = "AccountIds is required." });
            }

            var normalizedIds = request.AccountIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
            {
                return BadRequest(new { message = "AccountIds contains no valid IDs." });
            }

            if (normalizedIds.Count > _onlinePresenceOptions.SnapshotMaxAccountIds)
            {
                return BadRequest(new
                {
                    message = $"Too many account IDs. Maximum allowed is {_onlinePresenceOptions.SnapshotMaxAccountIds}."
                });
            }

            var rateLimitResult = await _onlinePresenceService.TryConsumeSnapshotRateLimitAsync(
                viewerAccountId.Value,
                DateTime.UtcNow,
                cancellationToken);

            if (!rateLimitResult.Allowed)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = "Too many presence snapshot requests. Please retry shortly.",
                    retryAfterSeconds = rateLimitResult.RetryAfterSeconds
                });
            }

            var result = await _onlinePresenceService.GetSnapshotAsync(
                viewerAccountId.Value,
                normalizedIds,
                DateTime.UtcNow,
                cancellationToken);

            return Ok(result);
        }
    }
}
