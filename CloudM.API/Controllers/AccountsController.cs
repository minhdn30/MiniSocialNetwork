using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.AccountSettingDTOs;
using CloudM.Application.Helpers.ClaimHelpers;
using CloudM.Application.Services.AccountServices;
using CloudM.Application.Services.AccountSettingServices;
using CloudM.Domain.Enums;

namespace CloudM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly IAccountSettingService _accountSettingService;

        private static bool IsValidEnumValue<TEnum>(TEnum value) where TEnum : struct, Enum
        {
            return Enum.IsDefined(typeof(TEnum), value);
        }

        public AccountsController(IAccountService accountService, IAccountSettingService accountSettingService)
        {
            _accountService = accountService;
            _accountSettingService = accountSettingService;
        }
        [Authorize(Roles = "Admin")]
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAccounts([FromQuery] AccountPagingRequest request)
        {
            var result = await _accountService.GetAccountsAsync(request);
            return Ok(result);
        }
        [Authorize(Roles = "Admin")]
        [HttpGet("{accountId}")]
        public async Task<ActionResult<AccountDetailResponse>> GetAccountByGuid([FromRoute] Guid accountId)
        {
            var result = await _accountService.GetAccountByGuid(accountId);
            return Ok(result);
        }
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<AccountDetailResponse>> AddAccount([FromBody] AccountCreateRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Request is required." });

            if (!Enum.IsDefined(typeof(RoleEnum), request.RoleId))
                return BadRequest(new { message = "Invalid RoleEnum value." });

            var result = await _accountService.CreateAccount(request);
            return CreatedAtAction(nameof(GetAccountByGuid), new {accountId = result.AccountId}, result);
        }
        [Authorize(Roles = "Admin")]
        [HttpPut("{accountId}")]
        public async Task<ActionResult<AccountDetailResponse>> UpdateAccount([FromRoute] Guid accountId, [FromBody] AccountUpdateRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Request is required." });

            if (request.RoleId.HasValue && !Enum.IsDefined(typeof(RoleEnum), request.RoleId.Value))
                return BadRequest(new { message = "Invalid RoleEnum value." });

            var result = await _accountService.UpdateAccount(accountId, request);
            return Ok(result); 
        }
        [Authorize]
        [HttpPatch("profile")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<AccountDetailResponse>> PatchAccountProfile([FromForm] ProfileUpdateRequest request)
        {
            var accountId = User.GetAccountId();
            if (accountId == null) return Unauthorized();

            if (request == null)
                return BadRequest(new { message = "Request is required." });

            if (request.PhonePrivacy.HasValue && !IsValidEnumValue(request.PhonePrivacy.Value))
                return BadRequest(new { message = "Invalid AccountPrivacyEnum value." });

            if (request.AddressPrivacy.HasValue && !IsValidEnumValue(request.AddressPrivacy.Value))
                return BadRequest(new { message = "Invalid AccountPrivacyEnum value." });

            var result = await _accountService.UpdateAccountProfile(accountId.Value, request);
            return Ok(result);
        }


        [Authorize]
        [HttpGet("settings")]
        public async Task<ActionResult<AccountSettingsResponse>> GetAccountSettings()
        {
            var accountId = User.GetAccountId();
            if (accountId == null) return Unauthorized();
            var result = await _accountSettingService.GetSettingsByAccountIdAsync(accountId.Value);
            return Ok(result);
        }

        [Authorize]
        [HttpPatch("settings")]
        public async Task<ActionResult<AccountSettingsResponse>> PatchAccountSettings([FromBody] AccountSettingsUpdateRequest request)
        {
            var accountId = User.GetAccountId();
            if (accountId == null) return Unauthorized();
            if (request == null)
                return BadRequest(new { message = "Request is required." });

            if (request.PhonePrivacy.HasValue && !IsValidEnumValue(request.PhonePrivacy.Value))
                return BadRequest(new { message = "Invalid AccountPrivacyEnum value for PhonePrivacy." });

            if (request.AddressPrivacy.HasValue && !IsValidEnumValue(request.AddressPrivacy.Value))
                return BadRequest(new { message = "Invalid AccountPrivacyEnum value for AddressPrivacy." });

            if (request.DefaultPostPrivacy.HasValue && !IsValidEnumValue(request.DefaultPostPrivacy.Value))
                return BadRequest(new { message = "Invalid PostPrivacyEnum value." });

            if (request.FollowerPrivacy.HasValue && !IsValidEnumValue(request.FollowerPrivacy.Value))
                return BadRequest(new { message = "Invalid AccountPrivacyEnum value for FollowerPrivacy." });

            if (request.FollowingPrivacy.HasValue && !IsValidEnumValue(request.FollowingPrivacy.Value))
                return BadRequest(new { message = "Invalid AccountPrivacyEnum value for FollowingPrivacy." });

            if (request.StoryHighlightPrivacy.HasValue && !IsValidEnumValue(request.StoryHighlightPrivacy.Value))
                return BadRequest(new { message = "Invalid AccountPrivacyEnum value for StoryHighlightPrivacy." });

            if (request.GroupChatInvitePermission.HasValue && !IsValidEnumValue(request.GroupChatInvitePermission.Value))
                return BadRequest(new { message = "Invalid GroupChatInvitePermissionEnum value." });

            if (request.OnlineStatusVisibility.HasValue && !IsValidEnumValue(request.OnlineStatusVisibility.Value))
                return BadRequest(new { message = "Invalid OnlineStatusVisibilityEnum value." });

            var result = await _accountSettingService.UpdateSettingsAsync(accountId.Value, request);
            return Ok(result);
        }

        [Authorize]
        [HttpPut("profile/{accountId}")]
        [Consumes("multipart/form-data")]

        public async Task<ActionResult<AccountDetailResponse>> UpdateAccountProfile([FromForm] ProfileUpdateRequest request)
        {
            var accountId = User.GetAccountId();
            if (accountId == null) return Unauthorized();

            if (request == null)
                return BadRequest(new { message = "Request is required." });

            if (request.PhonePrivacy.HasValue && !IsValidEnumValue(request.PhonePrivacy.Value))
                return BadRequest(new { message = "Invalid AccountPrivacyEnum value." });

            if (request.AddressPrivacy.HasValue && !IsValidEnumValue(request.AddressPrivacy.Value))
                return BadRequest(new { message = "Invalid AccountPrivacyEnum value." });

            var result = await _accountService.UpdateAccountProfile(accountId.Value, request);
            return Ok(result);
        }
        [HttpGet("profile/{accountId}")]
        public async Task<ActionResult<ProfileInfoResponse>> GetAccountProfileByGuid([FromRoute] Guid accountId)
        {
            var currentId = User.GetAccountId();
            var result = await _accountService.GetAccountProfileByGuid(accountId, currentId);
            return Ok(result);
        }

        [HttpGet("profile/username/{username}")]
        public async Task<ActionResult<ProfileInfoResponse>> GetAccountProfileByUsername([FromRoute] string username)
        {
            var currentId = User.GetAccountId();
            var result = await _accountService.GetAccountProfileByUsername(username, currentId);
            return Ok(result);
        }

        [HttpGet("profile-preview/{accountId}")]
        public async Task<IActionResult> GetAccountProfilePreview([FromRoute] Guid accountId)
        {
            var currentId = User.GetAccountId();
            var result = await _accountService.GetAccountProfilePreview(accountId, currentId);
            if(result == null) return NotFound(new {message = "Account not found."});
            return Ok(result);
        }
        

        [Authorize]
        [HttpPost("reactivate")]
        public async Task<IActionResult> Reactivate()
        {
            var accountId = User.GetAccountId();
            if (accountId == null) return Unauthorized();
            await _accountService.ReactivateAccountAsync(accountId.Value);
            return Ok(new { message = "Account reactivated successfully." });
        }
        //test get profile from token
        [Authorize]
        [HttpGet("profile-test")]
        public IActionResult GetProfile()
        {
            var accountId = User.GetAccountId();
            var username = User.GetUsername();
            var fullName = User.GetFullName();
            var avatar = User.GetAvatarUrl();
            var email = User.GetEmail();
            var role = User.GetRole();
            var isVerified = User.IsVerified();

            return Ok(new { accountId, username, fullName, avatar, email, role, isVerified });
        }
    }
}
