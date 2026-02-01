using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SocialNetwork.API.Hubs;
using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.DTOs.PostMediaDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.AccountServices;
using SocialNetwork.Application.Services.CommentServices;
using SocialNetwork.Application.Services.PostReactServices;
using SocialNetwork.Application.Services.PostServices;
using SocialNetwork.Infrastructure.Models;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly IPostReactService _postReactService;
        private readonly ICommentService _commentService;
        private readonly IHubContext<PostHub> _hubContext;
        public PostsController(IPostService postService, IPostReactService postReactService, ICommentService commentService, IHubContext<PostHub> hubContext)
        {
            _postService = postService;
            _postReactService = postReactService;
            _commentService = commentService;
            _hubContext = hubContext;
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
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<PostDetailResponse>> CreatePost([FromForm] PostCreateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });

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

            var result = await _postService.UpdatePost(postId, currentId.Value, request);
            //send signalR notification to FE
            await _hubContext.Clients.Group($"Post-{postId}").SendAsync("ReceiveUpdatedPost", result);
            await _hubContext.Clients.Group($"PostList-{currentId.Value}").SendAsync("ReceiveUpdatedPost", result);

            return Ok(result);
        }
        [Authorize]
        [HttpDelete("{postId}")]
        public async Task<IActionResult> SoftDeletePost([FromRoute] Guid postId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var accountId = await _postService.SoftDeletePost(postId, currentId.Value, User.IsAdmin());
            //send signalR notification to FE
            await _hubContext.Clients.Group($"Post-{postId}").SendAsync("ReceiveDeletedPost", postId);
            if (accountId != null)
                await _hubContext.Clients.Group($"PostList-{accountId}").SendAsync("ReceiveDeletedPost", postId);
            return NoContent();
        }
        [HttpGet("personal/{accountId}")]
        public async Task<ActionResult<PagedResponse<PostPersonalListModel>>> GetPostsByAccountId([FromRoute] Guid accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var currentId = User.GetAccountId();
            var result = await _postService.GetPostsByAccountId(accountId, currentId, page, pageSize);
            return Ok(result);
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
            await _hubContext.Clients.Group($"Post-{postId}").SendAsync("ReceiveReactUpdate", postId, result.ReactCount);
            return Ok(result);
        }
        [HttpGet("{postId}/reacts")]
        public async Task<ActionResult<PagedResponse<AccountReactListModel>>> GetPostReacts(Guid postId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _postReactService.GetAccountsReactOnPostPaged(postId, page, pageSize);
            return Ok(result);
        }

        
    }
}
