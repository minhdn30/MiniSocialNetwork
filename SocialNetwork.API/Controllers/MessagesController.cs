using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.ConversationMemberServices;
using SocialNetwork.Application.Services.ConversationServices;
using SocialNetwork.Application.Services.MessageServices;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageService _messageService;
        private readonly IConversationService _conversationService;
        private readonly IConversationMemberService _conversationMemberService;
        public MessagesController(IMessageService messageService, IConversationService conversationService, IConversationMemberService conversationMemberService)
        {
            _messageService = messageService;
            _conversationService = conversationService;
            _conversationMemberService = conversationMemberService;
        }
        [Authorize]
        [HttpGet("{conversationId}")]
        public async Task<IActionResult> GetMessagesByConversationId(Guid conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _messageService.GetMessagesByConversationIdAsync(conversationId, currentId.Value, page, pageSize);
            return Ok(result);
        }
    }
}
