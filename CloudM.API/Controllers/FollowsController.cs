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
        [HttpGet("requests")]
        public async Task<IActionResult> GetPendingFollowRequests([FromQuery] FollowRequestCursorRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var safeRequest = request ?? new FollowRequestCursorRequest();
            if (safeRequest.CursorCreatedAt.HasValue != safeRequest.CursorRequesterId.HasValue)
            {
                return BadRequest(new { message = "cursorCreatedAt and cursorRequesterId must be provided together." });
            }

            if (safeRequest.Limit > 100)
            {
                safeRequest.Limit = 100;
            }

            var result = await _followService.GetPendingRequestsAsync(currentId.Value, safeRequest);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("requests/sent")]
        public async Task<IActionResult> GetSentFollowRequests([FromQuery] FollowPagingRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var safeRequest = request ?? new FollowPagingRequest();
            if (safeRequest.PageSize > 50)
            {
                safeRequest.PageSize = 50;
            }

            var result = await _followService.GetSentPendingRequestsAsync(currentId.Value, safeRequest);
            return Ok(result);
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
        [HttpDelete("followers/{followerId}")]
        public async Task<IActionResult> RemoveFollower(Guid followerId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            if (followerId == Guid.Empty) return BadRequest(new { message = "Follower account is required." });
            if (followerId == currentId.Value) return BadRequest(new { message = "Invalid follower removal." });

            await _followService.RemoveFollowerAsync(currentId.Value, followerId);
            return Ok(new { message = "Follower removed." });
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

        [Authorize]
        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestions([FromQuery] FollowSuggestionPagingRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var result = await _followService.GetSuggestionsAsync(currentId.Value, request ?? new FollowSuggestionPagingRequest());
            return Ok(result);
        }
    }
}
