using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.FollowDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.AccountServices;
using SocialNetwork.Application.Services.FollowServices;

namespace SocialNetwork.API.Controllers
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
            var result = await _followService.FollowAsync(currentId.Value, targetId);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("{targetId}")]
        public async Task<IActionResult> Unfollow(Guid targetId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });

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

            var result = await _followService.IsFollowingAsync(currentId.Value, targetId);

            return Ok(new { isFollowing = result });
        }

        [HttpGet("followers")]
        public async Task<IActionResult> GetFollowers([FromQuery] Guid accountId, [FromQuery] FollowPagingRequest request)
        {
            var currentId = User.GetAccountId();
            var result = await _followService.GetFollowersAsync(accountId, currentId, request);
            return Ok(result);
        }

        [HttpGet("following")]
        public async Task<IActionResult> GetFollowing([FromQuery] Guid accountId, [FromQuery] FollowPagingRequest request)
        {
            var currentId = User.GetAccountId();
            var result = await _followService.GetFollowingAsync(accountId, currentId, request);
            return Ok(result);
        }
    }
}
