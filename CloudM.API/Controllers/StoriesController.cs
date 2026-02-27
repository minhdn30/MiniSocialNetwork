using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.StoryDTOs;
using CloudM.Application.DTOs.StoryHighlightDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.StoryHighlightServices;
using CloudM.Application.Services.StoryServices;
using CloudM.Application.Services.StoryViewServices;
using CloudM.Domain.Enums;

namespace CloudM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StoriesController : ControllerBase
    {
        private readonly IStoryService _storyService;
        private readonly IStoryHighlightService _storyHighlightService;
        private readonly IStoryViewService _storyViewService;

        public StoriesController(
            IStoryService storyService,
            IStoryHighlightService storyHighlightService,
            IStoryViewService storyViewService)
        {
            _storyService = storyService;
            _storyHighlightService = storyHighlightService;
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

            var result = await _storyService.GetViewableAuthorsAsync(currentId.Value, page, pageSize);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("archive")]
        public async Task<ActionResult<PagedResponse<StoryArchiveItemResponse>>> GetArchivedStories(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var result = await _storyService.GetArchivedStoriesAsync(currentId.Value, page, pageSize);
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

            var result = await _storyService.GetActiveStoriesByAuthorAsync(currentId.Value, authorId);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{storyId}/resolve")]
        public async Task<ActionResult<StoryResolveResponse>> ResolveStory([FromRoute] Guid storyId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            if (storyId == Guid.Empty)
            {
                return BadRequest(new { message = "StoryId is required." });
            }

            var result = await _storyService.ResolveStoryAsync(currentId.Value, storyId);
            if (result == null)
            {
                return NotFound(new { message = "Story not found or expired." });
            }

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

        [AllowAnonymous]
        [HttpGet("highlights/profiles/{targetAccountId}/groups")]
        public async Task<ActionResult<List<StoryHighlightGroupListItemResponse>>> GetProfileHighlightGroups([FromRoute] Guid targetAccountId)
        {
            var currentId = User.GetAccountId();
            var result = await _storyHighlightService.GetProfileHighlightGroupsAsync(targetAccountId, currentId);
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("highlights/profiles/{targetAccountId}/groups/{groupId}/stories")]
        public async Task<ActionResult<StoryHighlightGroupStoriesResponse>> GetHighlightGroupStories(
            [FromRoute] Guid targetAccountId,
            [FromRoute] Guid groupId)
        {
            var currentId = User.GetAccountId();
            var result = await _storyHighlightService.GetHighlightGroupStoriesAsync(targetAccountId, groupId, currentId);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("highlights/archive-candidates")]
        public async Task<ActionResult<PagedResponse<StoryHighlightArchiveCandidateResponse>>> GetHighlightArchiveCandidates(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? excludeGroupId = null)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var result = await _storyHighlightService.GetArchiveCandidatesAsync(currentId.Value, page, pageSize, excludeGroupId);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("highlights/groups")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<StoryHighlightGroupMutationResponse>> CreateHighlightGroup([FromForm] StoryHighlightCreateGroupRequest request)
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

            var result = await _storyHighlightService.CreateGroupAsync(currentId.Value, request);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        [Authorize]
        [HttpPost("highlights/groups/{groupId}/items")]
        public async Task<ActionResult<StoryHighlightGroupMutationResponse>> AddHighlightItems(
            [FromRoute] Guid groupId,
            [FromBody] StoryHighlightAddItemsRequest request)
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

            var result = await _storyHighlightService.AddItemsAsync(currentId.Value, groupId, request);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("highlights/groups/{groupId}/items/{storyId}")]
        public async Task<IActionResult> RemoveHighlightItem([FromRoute] Guid groupId, [FromRoute] Guid storyId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            await _storyHighlightService.RemoveItemAsync(currentId.Value, groupId, storyId);
            return NoContent();
        }

        [Authorize]
        [HttpPatch("highlights/groups/{groupId}")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<StoryHighlightGroupMutationResponse>> UpdateHighlightGroup(
            [FromRoute] Guid groupId,
            [FromForm] StoryHighlightUpdateGroupRequest request)
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

            var result = await _storyHighlightService.UpdateGroupAsync(currentId.Value, groupId, request);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("highlights/groups/{groupId}")]
        public async Task<IActionResult> DeleteHighlightGroup([FromRoute] Guid groupId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            await _storyHighlightService.DeleteGroupAsync(currentId.Value, groupId);
            return NoContent();
        }
    }
}
