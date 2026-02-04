using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.Helpers.ClaimHelpers;
using SocialNetwork.Application.Services.AccountServices;

namespace SocialNetwork.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountService _accountService;
        public AccountsController(IAccountService accountService)
        {
            _accountService = accountService;
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
            var result = await _accountService.CreateAccount(request);
            return CreatedAtAction(nameof(GetAccountByGuid), new {accountId = result.AccountId}, result);
        }
        [Authorize(Roles = "Admin")]
        [HttpPut("{accountId}")]
        public async Task<ActionResult<AccountDetailResponse>> UpdateAccount([FromRoute] Guid accountId, [FromBody] AccountUpdateRequest request)
        {
            var result = await _accountService.UpdateAccount(accountId, request);
            return Ok(result); 
        }
        [Authorize]
        [HttpPut("profile/{accountId}")]
        [Consumes("multipart/form-data")]

        public async Task<ActionResult<AccountDetailResponse>> UpdateAccountProfile([FromForm] ProfileUpdateRequest request)
        {
            var accountId = User.GetAccountId();
            if (accountId == null) return Unauthorized();
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
