using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.FollowDTOs;
using SocialNetwork.Application.Interfaces;
using System.Security.Claims;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FollowsController : ControllerBase
    {
        private readonly IFollowService _followService;
        private readonly IAccountService _accountService;
        public FollowsController(IFollowService followService, IAccountService accountService)
        {
            _followService = followService;
            _accountService = accountService;
        }
        //user
        [HttpPost("{targetId}")]
        public async Task<IActionResult> Follow(Guid targetId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var followerId = Guid.Parse(userIdClaim);
            await _followService.FollowAsync(followerId, targetId);

            return Ok(new { message = "Followed successfully." });
        }
        //user
        [HttpDelete("{targetId}")]
        public async Task<IActionResult> Unfollow(Guid targetId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }
            var followerId = Guid.Parse(userIdClaim);

            await _followService.UnfollowAsync(followerId, targetId);
            return Ok(new { message = "Unfollowed successfully." });
        }

        //check follow status when viewing another user's profile
        //user
        [HttpGet("status/{targetId}")]
        public async Task<IActionResult> FollowStatus(Guid targetId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var followerId = Guid.Parse(userIdClaim);

            var result = await _followService.IsFollowingAsync(followerId, targetId);

            return Ok(new { isFollowing = result });
        }
        [HttpGet("followers")]
        public async Task<IActionResult> GetFollowers([FromQuery] Guid? accountId, [FromQuery] FollowPagingRequest request)
        {
            Guid userToQuery;

            if (accountId.HasValue && accountId.Value != Guid.Empty)
            {
                // View other people's following list
                userToQuery = accountId.Value;
            }
            else
            {
                // Get from token
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Invalid token: no AccountId found." });

                userToQuery = Guid.Parse(userIdClaim);
            }

            var result = await _followService.GetFollowersAsync(userToQuery, request);

            return Ok(result);
        }
        [HttpGet("following")]
        public async Task<IActionResult> GetFollowing([FromQuery] Guid? accountId, [FromQuery] FollowPagingRequest request)
        {
            Guid userToQuery;

            if (accountId.HasValue && accountId.Value != Guid.Empty)
            {
                // View other people's following list
                userToQuery = accountId.Value;
            }
            else
            {
                // Get from token
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Invalid token: no AccountId found." });

                userToQuery = Guid.Parse(userIdClaim);
            }

            var result = await _followService.GetFollowingAsync(userToQuery, request);

            return Ok(result);
        }

    }
}
