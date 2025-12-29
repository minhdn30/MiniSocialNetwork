using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using SocialNetwork.Application.DTOs.ConversationMemberDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.AccountServices;
using SocialNetwork.Application.Services.ConversationMemberServices;
using SocialNetwork.Application.Services.ConversationServices;
using SocialNetwork.Application.Services.MessageServices;
using SocialNetwork.Infrastructure.Models;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationService _conversationService;
        private readonly IConversationMemberService _conversationMemberService;
        private readonly IMessageService _messageService;
        public ConversationsController(IConversationService conversationService, IConversationMemberService conversationMemberService,
            IMessageService messageService)
        {
            _conversationService = conversationService;
            _conversationMemberService = conversationMemberService;
            _messageService = messageService;
        }
        [Authorize]
        [HttpGet("private")]
        public async Task<IActionResult> GetPrivateConversation([FromQuery] Guid otherId)
        {
            var currentId = User.GetAccountId();
            if(currentId == null) 
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var conversation = await _conversationService.GetPrivateConversationAsync(currentId.Value, otherId);
            return Ok(conversation);
        }
        [Authorize]
        [HttpGet("private/{otherId}")]
        public async Task<IActionResult> GetPrivateConversationIncludeMessages([FromQuery] Guid otherId, [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var conversation = await _conversationService.GetPrivateConversationAsync(currentId.Value, otherId);
            if (conversation == null)
            {
                return Ok(new PrivateConversationIncludeMessagesResponse
                {
                    IsNew = true
                });
            }
            var pagedMessages = await _messageService.GetMessagesByConversationIdAsync(conversation.ConversationId, currentId.Value, page, pageSize);
            return Ok(new PrivateConversationIncludeMessagesResponse
            {
                IsNew = false,
                Conversation = conversation,
                Messages = pagedMessages
            });
        }

        [Authorize]
        [HttpPost("private")]
        public async Task<IActionResult> CreatePrivateConversation([FromBody] CreatePrivateConversationRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var conversation = await _conversationService.CreatePrivateConversationAsync(currentId.Value, request.OtherId);
            return CreatedAtAction(nameof(GetPrivateConversation), new { otherId = request.OtherId }, conversation);
        }
        [Authorize]
        [HttpPatch("{conversationId}/members/nickname")]
        public async Task<IActionResult> UpdateMemberNickname([FromRoute] Guid conversationId, [FromBody] ConversationMemberNicknameUpdateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            await _conversationMemberService.UpdateMemberNickname(conversationId, currentId.Value, request);
            return NoContent();
        }
        [Authorize]
        [HttpDelete("{conversationId}/history")]
        public async Task<IActionResult> SoftDeleteChatHistory([FromRoute] Guid conversationId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            await _conversationMemberService.SoftDeleteChatHistory(conversationId, currentId.Value);
            return NoContent();
        }
    }
}
