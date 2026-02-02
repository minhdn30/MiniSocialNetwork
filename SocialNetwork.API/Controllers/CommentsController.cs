using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SocialNetwork.API.Hubs;
using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.CommentReactServices;
using SocialNetwork.Application.Services.CommentServices;
using SocialNetwork.Application.Services.PostReactServices;
using SocialNetwork.Application.Services.PostServices;
using SocialNetwork.Infrastructure.Models;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentService _commentService;
        private readonly ICommentReactService _commentReactService;
        private readonly IHubContext<PostHub> _hubContext;
        public CommentsController(ICommentService commentService, ICommentReactService commentReactService, IHubContext<PostHub> hubContext)
        {
            _commentService = commentService;
            _commentReactService = commentReactService;
            _hubContext = hubContext;
        }
        //Comment
        [Authorize]
        [HttpPost("{postId}")]
        public async Task<ActionResult<CommentResponse>> AddComment([FromRoute] Guid postId, [FromBody] CommentCreateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _commentService.AddCommentAsync(postId, currentId.Value, request);

            int? parentReplyCount = null;
            if (result.ParentCommentId.HasValue)
            {
                parentReplyCount = await _commentService.GetReplyCountAsync(result.ParentCommentId.Value);
            }

            // Notify post group: new comment object & parent reply count
            await _hubContext.Clients.Group($"Post-{postId}").SendAsync("ReceiveNewComment", result, parentReplyCount);

            return Ok(result);
        }
        [Authorize]
        [HttpPut("{commentId}")]
        public async Task<ActionResult<CommentResponse>> UpdateComment([FromRoute] Guid commentId, [FromBody] CommentUpdateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _commentService.UpdateCommentAsync(commentId, currentId.Value, request);
            //send signalR notification to FE
            await _hubContext.Clients.Group($"Post-{result.PostId}").SendAsync("ReceiveUpdatedComment", result);
            return Ok(result);
        }
        [Authorize]
        [HttpDelete("{commentId}")]
        public async Task<IActionResult> DeleteComment([FromRoute] Guid commentId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var postId = await _commentService.DeleteCommentAsync(commentId, currentId.Value, User.IsAdmin());
            //send signalR notification to FE
            await _hubContext.Clients.Group($"Post-{postId}").SendAsync("ReceiveDeletedComment", commentId);
            return NoContent();
        }
        [Authorize]
        [HttpGet("post/{postId}")]
        public async Task<ActionResult<PagedResponse<CommentWithReplyCountModel>>> GetCommentsByPostId([FromRoute] Guid postId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var currentId = User.GetAccountId();
            var result = await _commentService.GetCommentsByPostIdAsync(postId, currentId, page, pageSize);
            return Ok(result);
        }
        [Authorize]
        [HttpGet("replies/{commentId}")]
        public async Task<ActionResult<PagedResponse<CommentWithReplyCountModel>>> GetRepliesByCommentId([FromRoute] Guid commentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var currentId = User.GetAccountId();
            var result = await _commentService.GetRepliesByCommentIdAsync(commentId, currentId, page, pageSize);
            return Ok(result);
        }
        //React
        [Authorize]
        [HttpPost("{commentId}/react")]
        public async Task<IActionResult> ToggleReactOnComment([FromRoute] Guid commentId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _commentReactService.ToggleReactOnComment(commentId, currentId.Value);

            // Notify group about comment/reply react count update
            var comment = await _commentService.GetCommentByIdAsync(commentId);
            if (comment != null)
            {
                await _hubContext.Clients.Group($"Post-{comment.PostId}").SendAsync("ReceiveCommentReactUpdate", commentId, result.ReactCount);
            }

            return Ok(result);
        }
        [HttpGet("{commentId}/reacts")]
        public async Task<ActionResult<PagedResponse<AccountReactListModel>>> GetAccountsReactOnCommentPaged([FromRoute] Guid commentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _commentReactService.GetAccountsReactOnCommentPaged(commentId, page, pageSize);
            return Ok(result);
        }
    }
}
