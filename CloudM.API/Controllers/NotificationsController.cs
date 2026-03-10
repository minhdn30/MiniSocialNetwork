using CloudM.Application.DTOs.NotificationDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.NotificationServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] NotificationCursorRequest request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var safeRequest = request ?? new NotificationCursorRequest();
            if (safeRequest.CursorLastEventAt.HasValue != safeRequest.CursorNotificationId.HasValue)
            {
                return BadRequest(new { message = "cursorLastEventAt and cursorNotificationId must be provided together." });
            }

            if (safeRequest.Limit > 100)
            {
                safeRequest.Limit = 100;
            }

            var filter = (safeRequest.Filter ?? "all").Trim().ToLowerInvariant();
            safeRequest.Filter = filter is "all" or "unread" ? filter : "all";

            var result = await _notificationService.GetNotificationsAsync(currentId.Value, safeRequest);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var summary = await _notificationService.GetUnreadSummaryAsync(currentId.Value);
            return Ok(summary);
        }

        [Authorize]
        [HttpPost("read-state")]
        public async Task<IActionResult> UpdateReadState([FromBody] NotificationReadStateRequest? request)
        {
            var currentId = User.GetAccountId();
            if (currentId == null)
            {
                return Unauthorized(new { message = "Invalid token: no AccountId found." });
            }

            var summary = await _notificationService.UpdateReadStateAsync(
                currentId.Value,
                request ?? new NotificationReadStateRequest());
            return Ok(summary);
        }
    }
}
