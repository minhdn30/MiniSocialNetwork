using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.ConversationMemberServices;
using SocialNetwork.Application.Services.ConversationServices;
using SocialNetwork.Application.Services.MessageHiddenServices;
using SocialNetwork.Application.Services.MessageServices;
using SocialNetwork.Application.Services.PinnedMessageServices;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageService _messageService;
        private readonly IMessageHiddenService _messageHiddenService;
        private readonly IConversationService _conversationService;
        private readonly IConversationMemberService _conversationMemberService;
        private readonly IPinnedMessageService _pinnedMessageService;

        public MessagesController(
            IMessageService messageService, 
            IMessageHiddenService messageHiddenService,
            IConversationService conversationService, 
            IConversationMemberService conversationMemberService,
            IPinnedMessageService pinnedMessageService)
        {
            _messageService = messageService;
            _messageHiddenService = messageHiddenService;
            _conversationService = conversationService;
            _conversationMemberService = conversationMemberService;
            _pinnedMessageService = pinnedMessageService;
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

        [Authorize]
        [HttpPost("hide/{messageId}")]
        public async Task<IActionResult> HideMessage(Guid messageId)
        {
            var accountId = User.GetAccountId();
            if (accountId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            await _messageHiddenService.HideMessageAsync(messageId, accountId.Value);
            return Ok(new { message = "Message hidden successfully." });
        }

        [Authorize]
        [HttpPost("recall/{messageId}")]
        public async Task<IActionResult> RecallMessage(Guid messageId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var result = await _messageService.RecallMessageAsync(messageId, currentId.Value);
            return Ok(result);
        }

        // PINNED MESSAGES

        // Get all pinned messages for a conversation
        [Authorize]
        [HttpGet("pinned/{conversationId}")]
        public async Task<IActionResult> GetPinnedMessages(Guid conversationId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var result = await _pinnedMessageService.GetPinnedMessagesAsync(conversationId, currentId.Value);
            return Ok(result);
        }

        // Pin a message in a conversation
        [Authorize]
        [HttpPost("pin/{conversationId}/{messageId}")]
        public async Task<IActionResult> PinMessage(Guid conversationId, Guid messageId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            await _pinnedMessageService.PinMessageAsync(conversationId, messageId, currentId.Value);
            return Ok(new { message = "Message pinned successfully." });
        }

        // Unpin a message from a conversation
        [Authorize]
        [HttpDelete("unpin/{conversationId}/{messageId}")]
        public async Task<IActionResult> UnpinMessage(Guid conversationId, Guid messageId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            await _pinnedMessageService.UnpinMessageAsync(conversationId, messageId, currentId.Value);
            return Ok(new { message = "Message unpinned successfully." });
        }
    }
}
