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
        [HttpGet]
        public async Task<IActionResult> GetConversations([FromQuery] bool? isPrivate, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var chats = await _conversationService.GetConversationsPagedAsync(currentId.Value, isPrivate, search, page, pageSize);
            return Ok(chats);
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
        public async Task<IActionResult> GetPrivateConversationIncludeMessages([FromRoute] Guid otherId, [FromQuery] string? cursor = null,
            [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var result = await _conversationService.GetPrivateConversationWithMessagesByOtherIdAsync(currentId.Value, otherId, cursor, pageSize);
            return Ok(result);
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
        [HttpPost("group")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateGroupConversation([FromForm] CreateGroupConversationRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var conversation = await _conversationService.CreateGroupConversationAsync(currentId.Value, request);
            return Ok(conversation);
        }

        [Authorize]
        [HttpPatch("{conversationId:guid}/members/nickname")]
        public async Task<IActionResult> UpdateMemberNickname([FromRoute] Guid conversationId, [FromBody] ConversationMemberNicknameUpdateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            await _conversationMemberService.UpdateMemberNickname(conversationId, currentId.Value, request);
            return NoContent();
        }
        [Authorize]
        [HttpPatch("{conversationId:guid}/mute")]
        public async Task<IActionResult> UpdateMuteStatus([FromRoute] Guid conversationId, [FromBody] ConversationMuteUpdateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            await _conversationMemberService.SetMuteStatusAsync(conversationId, currentId.Value, request.IsMuted);
            return NoContent();
        }

        [Authorize]
        [HttpPatch("{conversationId:guid}/theme")]
        public async Task<IActionResult> UpdateTheme([FromRoute] Guid conversationId, [FromBody] ConversationThemeUpdateRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            await _conversationMemberService.SetThemeAsync(conversationId, currentId.Value, request);
            return NoContent();
        }

        [Authorize]
        [HttpDelete("{conversationId:guid}/history")]
        public async Task<IActionResult> SoftDeleteChatHistory([FromRoute] Guid conversationId)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            await _conversationMemberService.SoftDeleteChatHistory(conversationId, currentId.Value);
            return NoContent();
        }

        [Authorize]
        [HttpGet("{conversationId:guid}/messages")]
        public async Task<IActionResult> GetConversationMessages([FromRoute] Guid conversationId, [FromQuery] string? cursor = null, [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var result = await _conversationService.GetConversationMessagesWithMetaDataAsync(conversationId, currentId.Value, cursor, pageSize);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{conversationId:guid}/messages/context")]
        public async Task<IActionResult> GetMessageContext([FromRoute] Guid conversationId, [FromQuery] Guid messageId, [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var result = await _conversationService.GetMessageContextAsync(conversationId, currentId.Value, messageId, pageSize);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{conversationId:guid}/messages/search")]
        public async Task<IActionResult> SearchMessages([FromRoute] Guid conversationId, [FromQuery] string keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            if (string.IsNullOrWhiteSpace(keyword) || keyword.Trim().Length < 2)
                return BadRequest(new { message = "Keyword must be at least 2 characters." });

            var result = await _conversationService.SearchMessagesAsync(conversationId, currentId.Value, keyword, page, pageSize);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("accounts/search")]
        public async Task<IActionResult> SearchAccountsForGroupInvite([FromQuery] SearchGroupInviteAccountsRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var normalizedKeyword = request.Keyword?.Trim() ?? string.Empty;
            if (normalizedKeyword.Length > 0 && normalizedKeyword.Length < 2)
                return BadRequest(new { message = "Keyword must be empty or at least 2 characters." });

            var result = await _conversationService.SearchAccountsForGroupInviteAsync(
                currentId.Value,
                normalizedKeyword,
                request.ExcludeAccountIds,
                request.Limit);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("{conversationId:guid}/media")]
        public async Task<IActionResult> GetConversationMedia([FromRoute] Guid conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var result = await _conversationService.GetConversationMediaAsync(conversationId, currentId.Value, page, pageSize);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{conversationId:guid}/files")]
        public async Task<IActionResult> GetConversationFiles([FromRoute] Guid conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var result = await _conversationService.GetConversationFilesAsync(conversationId, currentId.Value, page, pageSize);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
                return Unauthorized(new { message = "Invalid token: no AccountId found." });

            var count = await _conversationService.GetUnreadConversationCountAsync(currentId.Value);
            return Ok(new { count });
        }
    }
}
