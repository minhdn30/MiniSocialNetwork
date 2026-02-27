using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.MessageDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.ConversationMemberServices;
using CloudM.Application.Services.ConversationServices;
using CloudM.Application.Services.MessageHiddenServices;
using CloudM.Application.Services.MessageReactServices;
using CloudM.Application.Services.MessageServices;
using CloudM.Application.Services.PinnedMessageServices;
using CloudM.Domain.Enums;

namespace CloudM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageService _messageService;
        private readonly IMessageReactService _messageReactService;
        private readonly IMessageHiddenService _messageHiddenService;
        private readonly IConversationService _conversationService;
        private readonly IConversationMemberService _conversationMemberService;
        private readonly IPinnedMessageService _pinnedMessageService;

        public MessagesController(
            IMessageService messageService, 
            IMessageReactService messageReactService,
            IMessageHiddenService messageHiddenService,
            IConversationService conversationService, 
            IConversationMemberService conversationMemberService,
            IPinnedMessageService pinnedMessageService)
        {
            _messageService = messageService;
            _messageReactService = messageReactService;
            _messageHiddenService = messageHiddenService;
            _conversationService = conversationService;
            _conversationMemberService = conversationMemberService;
            _pinnedMessageService = pinnedMessageService;
        }

        private static bool IsMessagePayloadEmpty(string? content, List<IFormFile>? mediaFiles)
        {
            return string.IsNullOrWhiteSpace(content) && (mediaFiles == null || !mediaFiles.Any());
        }

        [Authorize]
        [HttpGet("{conversationId}")]
        public async Task<IActionResult> GetMessagesByConversationId(Guid conversationId, [FromQuery] string? cursor = null, [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            var result = await _messageService.GetMessagesByConversationIdAsync(conversationId, currentId.Value, cursor, pageSize);
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

            if (request == null)
                return BadRequest(new { message = "Request is required." });

            if (request.ReceiverId == Guid.Empty)
                return BadRequest(new { message = "Receiver account is required." });

            if (senderId.Value == request.ReceiverId)
                return BadRequest(new { message = "You cannot send a message to yourself." });

            if (IsMessagePayloadEmpty(request.Content, request.MediaFiles))
                return BadRequest(new { message = "Message content and media files cannot both be empty." });

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

            if (request == null)
                return BadRequest(new { message = "Request is required." });

            if (IsMessagePayloadEmpty(request.Content, request.MediaFiles))
                return BadRequest(new { message = "Message content and media files cannot both be empty." });
            
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

        [Authorize]
        [HttpGet("media/{messageMediaId}/download-url")]
        public async Task<IActionResult> GetMediaDownloadUrl(Guid messageMediaId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var downloadUrl = await _messageService.GetMediaDownloadUrlAsync(messageMediaId, currentId.Value);
            return Ok(new { url = downloadUrl });
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
        [Authorize]
        [HttpGet("{messageId}/react")]
        public async Task<IActionResult> GetMessageReact(Guid messageId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var result = await _messageReactService.GetMessageReactStateAsync(messageId, currentId.Value);
            return Ok(result);
        }

        [Authorize]
        [HttpPut("{messageId}/react")]
        public async Task<IActionResult> SetMessageReact(Guid messageId, [FromBody] SetMessageReactRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            if (request == null)
                return BadRequest(new { message = "React payload is required." });
            if (!Enum.IsDefined(typeof(ReactEnum), request.ReactType))
                return BadRequest(new { message = "Invalid react type." });

            var result = await _messageReactService.SetMessageReactAsync(messageId, currentId.Value, request.ReactType);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("{messageId}/react")]
        public async Task<IActionResult> RemoveMessageReact(Guid messageId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var result = await _messageReactService.RemoveMessageReactAsync(messageId, currentId.Value);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("story-reply")]
        public async Task<IActionResult> SendStoryReply([FromBody] SendStoryReplyRequest request)
        {
            var senderId = User.GetAccountId();
            if (senderId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            if (request == null)
                return BadRequest(new { message = "Request is required." });

            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { message = "Reply content is required." });

            if (request.StoryId == Guid.Empty)
                return BadRequest(new { message = "Story ID is required." });

            if (request.ReceiverId == Guid.Empty)
                return BadRequest(new { message = "Receiver account is required." });

            if (senderId.Value == request.ReceiverId)
                return BadRequest(new { message = "You cannot reply to your own story." });

            var result = await _messageService.SendStoryReplyAsync(senderId.Value, request);
            return Ok(result);
        }

    }
}
