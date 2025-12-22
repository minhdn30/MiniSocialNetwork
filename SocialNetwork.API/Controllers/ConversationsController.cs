using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using SocialNetwork.Application.Services.AccountServices;
using SocialNetwork.Application.Services.ConversationMemberServices;
using SocialNetwork.Application.Services.ConversationServices;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationService _conversationService;
        private readonly IConversationMemberService _conversationMemberService;
        public ConversationsController(IConversationService conversationService, IConversationMemberService conversationMemberService)
        {
            _conversationService = conversationService;
            _conversationMemberService = conversationMemberService;
        }
        [HttpPost("private")]
        public async Task<IActionResult> GetOrCreatePrivateConversation([FromBody] ConversationGetOrCreateRequest request)
        {
            var conversation = await _conversationService.GetOrCreateConversationAsync(request.SenderId, request.ReceiverId);
            return Ok(conversation);
        }

    }
}
