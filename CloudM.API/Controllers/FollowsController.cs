using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.FollowDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.AccountServices;
using CloudM.Application.Services.FollowServices;

namespace CloudM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FollowsController : ControllerBase
    {
        private readonly IFollowService _followService;
        private readonly IAccountService _accountService;

        public FollowsController(
            IFollowService followService, 
            IAccountService accountService)
        {
            _followService = followService;
            _accountService = accountService;
        }

        [Authorize]
        [HttpPost("{targetId}")]
        public async Task<IActionResult> Follow(Guid targetId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            if (targetId == Guid.Empty) return BadRequest(new { message = "Target account is required." });
            if (currentId.Value == targetId) return BadRequest(new { message = "You cannot follow yourself." });
            var result = await _followService.FollowAsync(currentId.Value, targetId);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("{targetId}")]
        public async Task<IActionResult> Unfollow(Guid targetId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            if (targetId == Guid.Empty) return BadRequest(new { message = "Target account is required." });

            var result = await _followService.UnfollowAsync(currentId.Value, targetId);
            return Ok(result);
        }

        //check follow status when viewing another user's profile
        [Authorize]
        [HttpGet("status/{targetId}")]
        public async Task<IActionResult> FollowStatus(Guid targetId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            if (targetId == Guid.Empty) return BadRequest(new { message = "Target account is required." });

            var result = await _followService.GetRelationStatusAsync(currentId.Value, targetId);

            return Ok(new
            {
                followers = result.Followers,
                following = result.Following,
                isFollowedByCurrentUser = result.IsFollowedByCurrentUser,
                isFollowing = result.IsFollowedByCurrentUser,
                isFollowRequestPendingByCurrentUser = result.IsFollowRequestPendingByCurrentUser,
                isFollowRequestPending = result.IsFollowRequestPendingByCurrentUser,
                relationStatus = result.RelationStatus,
                targetFollowPrivacy = result.TargetFollowPrivacy
            });
        }

        [Authorize]
        [HttpPost("requests/{requesterId}/accept")]
        public async Task<IActionResult> AcceptFollowRequest(Guid requesterId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            if (requesterId == Guid.Empty) return BadRequest(new { message = "Requester account is required." });
            if (requesterId == currentId.Value) return BadRequest(new { message = "Invalid follow request." });

            await _followService.AcceptFollowRequestAsync(currentId.Value, requesterId);
            return Ok(new { message = "Follow request accepted." });
        }

        [Authorize]
        [HttpDelete("requests/{requesterId}")]
        public async Task<IActionResult> RemoveFollowRequest(Guid requesterId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            if (requesterId == Guid.Empty) return BadRequest(new { message = "Requester account is required." });
            if (requesterId == currentId.Value) return BadRequest(new { message = "Invalid follow request." });

            await _followService.RemoveFollowRequestAsync(currentId.Value, requesterId);
            return Ok(new { message = "Follow request removed." });
        }

        [Authorize]
        [HttpGet("followers")]
        public async Task<IActionResult> GetFollowers([FromQuery] Guid accountId, [FromQuery] FollowPagingRequest request)
        {
            var currentId = User.GetAccountId();
            var result = await _followService.GetFollowersAsync(accountId, currentId, request);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("following")]
        public async Task<IActionResult> GetFollowing([FromQuery] Guid accountId, [FromQuery] FollowPagingRequest request)
        {
            var currentId = User.GetAccountId();
            var result = await _followService.GetFollowingAsync(accountId, currentId, request);
            return Ok(result);
        }
    }
}
