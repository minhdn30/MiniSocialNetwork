using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AccountDTOs;
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
        //admin
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAccounts([FromQuery] AccountPagingRequest request)
        {
            var result = await _accountService.GetAccountsAsync(request);
            return Ok(result);
        }
        //admin
        [HttpGet("{accountId}")]
        public async Task<ActionResult<AccountDetailResponse>> GetAccountByGuid([FromRoute] Guid accountId)
        {
            var result = await _accountService.GetAccountByGuid(accountId);
            return Ok(result);
        }
        //admin
        [HttpPost]
        public async Task<ActionResult<AccountDetailResponse>> AddAccount([FromBody] AccountCreateRequest request)
        {
            var result = await _accountService.CreateAccount(request);
            return CreatedAtAction(nameof(GetAccountByGuid), new {accountId = result.AccountId}, result);
        }
        //admin
        [HttpPut("{accountId}")]
        public async Task<ActionResult<AccountDetailResponse>> UpdateAccount([FromRoute] Guid accountId, [FromBody] AccountUpdateRequest request)
        {
            var result = await _accountService.UpdateAccount(accountId, request);
            return Ok(result); 
        }
        //user
        [HttpPut("profile/{accountId}")]
        [Consumes("multipart/form-data")]

        public async Task<ActionResult<AccountDetailResponse>> UpdateAccountProfile([FromRoute] Guid accountId, [FromForm] ProfileUpdateRequest request)
        {
            var result = await _accountService.UpdateAccountProfile(accountId, request);
            return Ok(result);
        }
        // user
        [HttpGet("profile/{accountId}")]
        public async Task<ActionResult<AccountDetailResponse>> GetAccountProfileByGuid([FromRoute] Guid accountId)
        {
            var accountIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid? currentId = null;
            if (accountIdClaim != null && Guid.TryParse(accountIdClaim, out var parsedId))
            {
                currentId = parsedId;
            }

            var result = await _accountService.GetAccountProfileByGuid(accountId, currentId);
            return Ok(result);
        }
    }
}
