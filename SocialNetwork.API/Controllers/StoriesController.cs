using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.StoryDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.StoryServices;
using SocialNetwork.Application.Services.StoryViewServices;
using SocialNetwork.Domain.Enums;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StoriesController : ControllerBase
    {
        private readonly IStoryService _storyService;
        private readonly IStoryViewService _storyViewService;

        public StoriesController(IStoryService storyService, IStoryViewService storyViewService)
        {
            _storyService = storyService;
            _storyViewService = storyViewService;
        }

        [Authorize]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<StoryDetailResponse>> CreateStory([FromForm] StoryCreateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            if (!request.ContentType.HasValue)
            {
                return BadRequest(new { message = "ContentType is required." });
            }

            if (!Enum.IsDefined(typeof(StoryContentTypeEnum), request.ContentType.Value))
            {
                return BadRequest(new { message = "Invalid story content type." });
            }

            if (request.Privacy.HasValue && !Enum.IsDefined(typeof(StoryPrivacyEnum), request.Privacy.Value))
            {
                return BadRequest(new { message = "Invalid story privacy setting." });
            }

            if (!request.ExpiresEnum.HasValue || !Enum.IsDefined(typeof(StoryExpiresEnum), request.ExpiresEnum.Value))
            {
                request.ExpiresEnum = (int)StoryExpiresEnum.Hours24;
            }

            var result = await _storyService.CreateStoryAsync(currentId.Value, request);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        [Authorize]
        [HttpPatch("{storyId}/privacy")]
        public async Task<ActionResult<StoryDetailResponse>> UpdateStoryPrivacy([FromRoute] Guid storyId, [FromBody] StoryPrivacyUpdateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            if (!request.Privacy.HasValue)
            {
                return BadRequest(new { message = "Privacy is required." });
            }

            if (!Enum.IsDefined(typeof(StoryPrivacyEnum), request.Privacy.Value))
            {
                return BadRequest(new { message = "Invalid story privacy setting." });
            }

            var result = await _storyService.UpdateStoryPrivacyAsync(storyId, currentId.Value, request);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("{storyId}")]
        public async Task<IActionResult> SoftDeleteStory([FromRoute] Guid storyId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            await _storyService.SoftDeleteStoryAsync(storyId, currentId.Value);
            return NoContent();
        }

        [Authorize]
        [HttpGet("viewable-authors")]
        public async Task<ActionResult<PagedResponse<StoryAuthorItemResponse>>> GetViewableAuthors(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var result = await _storyViewService.GetViewableAuthorsAsync(currentId.Value, page, pageSize);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("authors/{authorId}/active")]
        public async Task<ActionResult<StoryAuthorActiveResponse>> GetActiveStoriesByAuthor([FromRoute] Guid authorId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var result = await _storyViewService.GetActiveStoriesByAuthorAsync(currentId.Value, authorId);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("views")]
        public async Task<ActionResult<StoryMarkViewedResponse>> MarkStoriesViewed([FromBody] StoryMarkViewedRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            var result = await _storyViewService.MarkStoriesViewedAsync(currentId.Value, request);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("{storyId}/react")]
        public async Task<ActionResult<StoryActiveItemResponse>> ReactStory([FromRoute] Guid storyId, [FromBody] StoryReactRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            if (request == null)
            {
                return BadRequest(new { message = "Request is required." });
            }

            var result = await _storyViewService.ReactStoryAsync(currentId.Value, storyId, request);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{storyId}/viewers")]
        public async Task<ActionResult<PagedResponse<StoryViewerBasicResponse>>> GetStoryViewers(
            [FromRoute] Guid storyId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var result = await _storyViewService.GetStoryViewersAsync(currentId.Value, storyId, page, pageSize);
            return Ok(result);
        }
    }
}
