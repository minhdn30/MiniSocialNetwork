using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.CommentDTOs;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.PostDTOs;
using CloudM.Application.DTOs.PostMediaDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.CommentServices;
using CloudM.Application.Services.PostReactServices;
using CloudM.Application.Services.PostSaveServices;
using CloudM.Application.Services.PostServices;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;

namespace CloudM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly IPostReactService _postReactService;
        private readonly IPostSaveService _postSaveService;
        private readonly ICommentService _commentService;

        public PostsController(
            IPostService postService, 
            IPostReactService postReactService, 
            IPostSaveService postSaveService,
            ICommentService commentService)
        {
            _postService = postService;
            _postReactService = postReactService;
            _postSaveService = postSaveService;
            _commentService = commentService;
        }

        [Authorize]
        [HttpGet("info/{postId}")]
        public async Task<ActionResult<PostDetailResponse>> GetPostById([FromRoute] Guid postId)
        {
            var currentId = User.GetAccountId();
            var result = await _postService.GetPostById(postId, currentId);
            return Ok(result);
        }

        //main detail api
        [Authorize]
        [HttpGet("{postId}")]
        public async Task<IActionResult> GetPostDetailAsync([FromRoute] Guid postId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _postService.GetPostDetailByPostId(postId, currentId.Value);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("p/{postCode}")]
        public async Task<IActionResult> GetPostDetailByPostCodeAsync([FromRoute] string postCode)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _postService.GetPostDetailByPostCode(postCode, currentId.Value);
            return Ok(result);
        }

        [Authorize]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<PostDetailResponse>> CreatePost([FromForm] PostCreateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });

            if (request == null)
                return BadRequest(new { message = "Request is required." });

            if (request.Privacy.HasValue && !Enum.IsDefined(typeof(PostPrivacyEnum), request.Privacy.Value))
                return BadRequest(new { message = "Invalid privacy setting." });

            if (request.FeedAspectRatio.HasValue && !Enum.IsDefined(typeof(AspectRatioEnum), request.FeedAspectRatio.Value))
                return BadRequest(new { message = "Invalid feed aspect ratio." });

            var result = await _postService.CreatePost(currentId.Value, request);
            return CreatedAtAction(nameof(GetPostById), new { postId = result.PostId }, result);
        }

        [Authorize]
        [HttpPut("{postId}")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<PostDetailResponse>> UpdatePost([FromForm] Guid postId, [FromForm] PostUpdateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            if (request == null)
                return BadRequest(new { message = "Request is required." });

            if (request.Privacy.HasValue && !Enum.IsDefined(typeof(PostPrivacyEnum), request.Privacy.Value))
                return BadRequest(new { message = "Invalid privacy setting." });

            var result = await _postService.UpdatePost(postId, currentId.Value, request);
            return Ok(result);
        }

        [Authorize]
        [HttpPatch("{postId}/content")]
        public async Task<ActionResult<PostUpdateContentResponse>> UpdatePostContent([FromRoute] Guid postId, [FromBody] PostUpdateContentRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            if (request == null)
                return BadRequest(new { message = "Request is required." });

            if (request.Privacy.HasValue && !Enum.IsDefined(typeof(PostPrivacyEnum), request.Privacy.Value))
                return BadRequest(new { message = "Invalid privacy setting." });

            var result = await _postService.UpdatePostContent(postId, currentId.Value, request);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("{postId}")]
        public async Task<IActionResult> SoftDeletePost([FromRoute] Guid postId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            await _postService.SoftDeletePost(postId, currentId.Value, User.IsAdmin());
            return NoContent();
        }

        [Authorize]
        [HttpGet("profile/{accountId}")]
        public async Task<IActionResult> GetPostsByAccountId(
            [FromRoute] Guid accountId,
            [FromQuery] int limit = 10,
            [FromQuery] DateTime? cursorCreatedAt = null,
            [FromQuery] Guid? cursorPostId = null)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            if (cursorCreatedAt.HasValue != cursorPostId.HasValue)
                return BadRequest(new { message = "cursorCreatedAt and cursorPostId must be provided together." });

            var result = await _postService.GetPostsByAccountIdByCursorAsync(
                accountId,
                currentId,
                cursorCreatedAt,
                cursorPostId,
                limit);

            DateTime? nextCursorCreatedAt = null;
            Guid? nextCursorPostId = null;
            if (result.HasMore && result.Items.Count > 0)
            {
                var lastItem = result.Items.Last();
                nextCursorCreatedAt = lastItem.CreatedAt;
                nextCursorPostId = lastItem.PostId;
            }

            return Ok(new
            {
                Items = result.Items,
                NextCursor = nextCursorCreatedAt.HasValue && nextCursorPostId.HasValue
                    ? new
                    {
                        CreatedAt = nextCursorCreatedAt,
                        PostId = nextCursorPostId
                    }
                    : null
            });
        }

        [Authorize]
        [HttpGet("saved")]
        public async Task<IActionResult> GetSavedPosts(
            [FromQuery] int limit = 12,
            [FromQuery] DateTime? cursorCreatedAt = null,
            [FromQuery] Guid? cursorPostId = null)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            if (cursorCreatedAt.HasValue != cursorPostId.HasValue)
                return BadRequest(new { message = "cursorCreatedAt and cursorPostId must be provided together." });

            var result = await _postSaveService.GetSavedPostsByCursorAsync(
                currentId.Value,
                cursorCreatedAt,
                cursorPostId,
                limit);
            var items = result.Items;

            DateTime? nextCursorCreatedAt = null;
            Guid? nextCursorPostId = null;
            if (items.Count > 0)
            {
                var lastItem = items.Last();
                nextCursorCreatedAt = lastItem.SavedAt;
                nextCursorPostId = lastItem.PostId;
            }

            return Ok(new
            {
                Items = items,
                NextCursor = result.HasMore && items.Count > 0 && nextCursorCreatedAt.HasValue
                    ? new
                    {
                        CreatedAt = nextCursorCreatedAt,
                        PostId = nextCursorPostId
                    }
                    : null
            });
        }

        [Authorize]
        [HttpGet("feed")]
        public async Task<IActionResult> GetFeedPostsByScore([FromQuery] int limit = 10, 
            [FromQuery] DateTime? cursorCreatedAt = null, [FromQuery] Guid? cursorPostId = null)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var feed = await _postService.GetFeedByScoreAsync(currentId.Value, cursorCreatedAt, cursorPostId, limit);
            DateTime? nextCursorCreatedAt = null;
            Guid? nextCursorPostId = null;
            if (feed.Any())
            {
                var lastItem = feed.Last();
                nextCursorCreatedAt = lastItem.CreatedAt;
                nextCursorPostId = lastItem.PostId;
            }
            return Ok(new
            {
                Items = feed,
                NextCursor = feed.Any()
                    ? new
                    {
                        CreatedAt = nextCursorCreatedAt,
                        PostId = nextCursorPostId
                    }
                    : null
            });
        }

        //React
        [Authorize]
        [HttpPost("{postId}/react")]
        public async Task<IActionResult> ToggleReact([FromRoute] Guid postId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _postReactService.ToggleReactOnPost(postId, currentId.Value);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("{postId}/save")]
        public async Task<IActionResult> SavePost([FromRoute] Guid postId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _postSaveService.SavePostAsync(currentId.Value, postId);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("{postId}/save")]
        public async Task<IActionResult> UnsavePost([FromRoute] Guid postId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _postSaveService.UnsavePostAsync(currentId.Value, postId);
            return Ok(result);
        }

        [HttpGet("{postId}/reacts")]
        public async Task<ActionResult<PagedResponse<AccountReactListModel>>> GetPostReacts([FromRoute] Guid postId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            var result = await _postReactService.GetAccountsReactOnPostPaged(postId, currentId, page, pageSize);
            return Ok(result);
        }
    }
}
