using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.DTOs.PostMediaDTOs;
using SocialNetwork.Application.Services.AccountServices;
using SocialNetwork.Application.Services.PostServices;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        public PostsController(IPostService postService)
        {
            _postService = postService;
        }
        [HttpGet("{postId}")]
        public async Task<ActionResult<PostDetailResponse>> GetPostById([FromRoute] Guid postId)
        {
            var result = await _postService.GetPostById(postId);
            return Ok(result);
        }
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<PostDetailResponse>> CreatePost([FromForm] PostCreateRequest request)
        {
            var result = await _postService.CreatePost(request);
            return CreatedAtAction(nameof(GetPostById), new { postId = result.PostId }, result);
        }
        [HttpPut("{postId}")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<PostDetailResponse>> UpdatePost([FromForm] Guid postId, [FromForm] PostUpdateRequest request)
        {
            var result = await _postService.UpdatePost(postId, request);
            return Ok(result);
        }
        [HttpDelete("{postId}")]
        public async Task<IActionResult> SoftDeletePost(Guid postId)
        {
            await _postService.SoftDeletePost(postId);
            return NoContent();            
        }

    }
}
