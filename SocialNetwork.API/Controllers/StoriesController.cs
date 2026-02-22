using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.StoryDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.StoryServices;
using SocialNetwork.Domain.Enums;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StoriesController : ControllerBase
    {
        private readonly IStoryService _storyService;

        public StoriesController(IStoryService storyService)
        {
            _storyService = storyService;
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
    }
}
