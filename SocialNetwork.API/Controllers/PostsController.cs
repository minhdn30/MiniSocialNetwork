using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SocialNetwork.API.Hubs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.DTOs.PostMediaDTOs;
using SocialNetwork.Application.Services.AccountServices;
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
        private readonly IHubContext<PostHub> _hubContext;
        public PostsController(IPostService postService, IPostReactService postReactService, IHubContext<PostHub> hubContext)
        {
            _postService = postService;
            _postReactService = postReactService;
            _hubContext = hubContext;
        }
        [HttpGet("{postId}")]
        public async Task<ActionResult<PostDetailResponse>> GetPostById([FromRoute] Guid postId)
        {
            var accountIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid? currentId = null;
            if (accountIdClaim != null && Guid.TryParse(accountIdClaim, out var parsedId))
            {
                currentId = parsedId;
            }
            var result = await _postService.GetPostById(postId, currentId);
            return Ok(result);
        }
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<PostDetailResponse>> CreatePost([FromForm] PostCreateRequest request)
        {
            var result = await _postService.CreatePost(request);
            if (result.Owner?.AccountId != null)
                //send signalR notification to FE
                await _hubContext.Clients.Group($"PostList-{result.Owner.AccountId}").SendAsync("ReceiveNewPost", result);

            return CreatedAtAction(nameof(GetPostById), new { postId = result.PostId }, result);
        }
        [HttpPut("{postId}")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<PostDetailResponse>> UpdatePost([FromForm] Guid postId, [FromForm] PostUpdateRequest request)
        {
            var accountIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid? currentId = null;
            if (accountIdClaim != null && Guid.TryParse(accountIdClaim, out var parsedId))
            {
                currentId = parsedId;
            }
            var result = await _postService.UpdatePost(postId, currentId, request);
            //send signalR notification to FE
            await _hubContext.Clients.Group($"Post-{postId}").SendAsync("ReceiveUpdatedPost", result);
            await _hubContext.Clients.Group($"PostList-{result.Owner.AccountId}").SendAsync("ReceiveUpdatedPost", result);

            return Ok(result);
        }
        [HttpDelete("{postId}")]
        public async Task<IActionResult> SoftDeletePost(Guid postId)
        {
            var accountId = await _postService.SoftDeletePost(postId);
            //send signalR notification to FE
            await _hubContext.Clients.Group($"Post-{postId}").SendAsync("ReceiveDeletedPost", postId);
            if (accountId != null)
                await _hubContext.Clients.Group($"PostList-{accountId}").SendAsync("ReceiveDeletedPost", postId);
            return NoContent();
        }
        [HttpGet("personal/{accountId}")]
        public async Task<ActionResult<PagedResponse<PostPersonalListModel>>> GetPostsByAccountId([FromRoute] Guid accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var accountIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid? currentId = null;

            if (accountIdClaim != null && Guid.TryParse(accountIdClaim, out var parsedId))
            {
                currentId = parsedId;
            }
            var result = await _postService.GetPostsByAccountId(accountId, currentId, page, pageSize);
            return Ok(result);
        }
        //React
        [HttpPost("{postId}/react")]
        public async Task<IActionResult> ToggleReact([FromRoute] Guid postId)
        {
            var accountIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (accountIdClaim == null || !Guid.TryParse(accountIdClaim, out var accountId))
            {
                return Unauthorized();
            }
            var result = await _postReactService.ToggleReact(postId, accountId);
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
