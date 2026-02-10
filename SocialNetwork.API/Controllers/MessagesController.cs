using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.MessageDTOs;
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

        public MessagesController(
            IMessageService messageService, 
            IConversationService conversationService, 
            IConversationMemberService conversationMemberService)
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

        [Authorize]
        [HttpPost("private-chat")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SendMessageInPrivateChat([FromForm] SendMessageInPrivateChatRequest request)
        {
            var senderId = User.GetAccountId();
            if (senderId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _messageService.SendMessageInPrivateChatAsync(senderId.Value, request);
            return Ok(result);
        }

        /// <summary>
        /// send message to group chat
        /// </summary>
        [Authorize]
        [HttpPost("group/{conversationId}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SendMessageInGroup(
            [FromRoute] Guid conversationId,
            [FromForm] SendMessageRequest request)
        {
            var senderId = User.GetAccountId();
            if (senderId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            
            var result = await _messageService.SendMessageInGroupAsync(senderId.Value, conversationId, request);
            return Ok(result);
        }
    }
}
