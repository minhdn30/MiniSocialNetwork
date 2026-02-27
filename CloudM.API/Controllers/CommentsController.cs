using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.CommentDTOs;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.PostDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.CommentReactServices;
using CloudM.Application.Services.CommentServices;
using CloudM.Infrastructure.Models;

namespace CloudM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentService _commentService;
        private readonly ICommentReactService _commentReactService;

        public CommentsController(
            ICommentService commentService, 
            ICommentReactService commentReactService)
        {
            _commentService = commentService;
            _commentReactService = commentReactService;
        }

        //Comment
        [Authorize]
        [HttpPost("{postId}")]
        public async Task<ActionResult<CommentResponse>> AddComment([FromRoute] Guid postId, [FromBody] CommentCreateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _commentService.AddCommentAsync(postId, currentId.Value, request);
            return Ok(result);
        }

        [Authorize]
        [HttpPut("{commentId}")]
        public async Task<ActionResult<CommentResponse>> UpdateComment([FromRoute] Guid commentId, [FromBody] CommentUpdateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var result = await _commentService.UpdateCommentAsync(commentId, currentId.Value, request);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("{commentId}")]
        public async Task<IActionResult> DeleteComment([FromRoute] Guid commentId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null) return Unauthorized(new { message = "Invalid token: no AccountId found." });
            await _commentService.DeleteCommentAsync(commentId, currentId.Value, User.IsAdmin());
            return NoContent();
        }

        [Authorize]
        [HttpGet("post/{postId}")]
        public async Task<ActionResult<PagedResponse<CommentResponse>>> GetCommentsByPostId([FromRoute] Guid postId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var currentId = User.GetAccountId();
            var result = await _commentService.GetCommentsByPostIdAsync(postId, currentId, page, pageSize);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("replies/{commentId}")]
        public async Task<ActionResult<PagedResponse<CommentResponse>>> GetRepliesByCommentId([FromRoute] Guid commentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
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
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{commentId}/reacts")]
        public async Task<ActionResult<PagedResponse<AccountReactListModel>>> GetAccountsReactOnCommentPaged([FromRoute] Guid commentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            var result = await _commentReactService.GetAccountsReactOnCommentPaged(commentId, currentId, page, pageSize);
            return Ok(result);
        }
    }
}
