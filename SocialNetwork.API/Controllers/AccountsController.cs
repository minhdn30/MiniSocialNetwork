using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.Interfaces;

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
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAccounts([FromQuery] AccountPagingRequest request)
        {
            var result = await _accountService.GetAccountsAsync(request);
            return Ok(result);
        }
        [HttpGet("{accountId}")]
        public async Task<ActionResult<AccountDetailResponse>> GetAccountByGuid([FromRoute] Guid accountId)
        {
            var result = await _accountService.GetAccountByGuid(accountId);
            return Ok(result);
        }
        [HttpPost]
        public async Task<ActionResult<AccountDetailResponse>> AddAccount([FromBody] AccountCreateRequest request)
        {
            var result = await _accountService.CreateAccount(request);
            return Ok(result);
        }
        [HttpPut("{accountId}")]
        public async Task<ActionResult<AccountDetailResponse>> UpdateAccount([FromRoute] Guid accountId, [FromBody] AccountUpdateRequest request)
        {
            var result = await _accountService.UpdateAccount(accountId, request);
            return Ok(result); 
        }
        [HttpPut("profile/{accountId}")]
        [Consumes("multipart/form-data")]

        public async Task<ActionResult<AccountDetailResponse>> UpdateAccountProfile([FromRoute] Guid accountId, [FromForm] ProfileUpdateRequest request)
        {
            var result = await _accountService.UpdateAccountProfile(accountId, request);
            return Ok(result);
        }

    }
}
