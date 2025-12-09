using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.AccountServices
{
    public interface IAccountService
    {
        Task<ActionResult<PagedResponse<AccountOverviewResponse>>> GetAccountsAsync([FromQuery] AccountPagingRequest request);
        Task<ActionResult<ProfileResponse?>> GetAccountByGuid(Guid accountId);
        Task<AccountDetailResponse> CreateAccount([FromBody] AccountCreateRequest request);
        Task<AccountDetailResponse> UpdateAccount(Guid accountId, [FromBody] AccountUpdateRequest request);
        Task<AccountDetailResponse> UpdateAccountProfile(Guid accountId, [FromBody] ProfileUpdateRequest request);
    }
}
