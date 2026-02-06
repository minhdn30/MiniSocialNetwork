using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.AccountServices
{
    public interface IAccountService
    {
        Task<ActionResult<PagedResponse<AccountOverviewResponse>>> GetAccountsAsync(AccountPagingRequest request);
        Task<ActionResult<AccountInfoResponse?>> GetAccountByGuid(Guid accountId);
        Task<AccountDetailResponse> CreateAccount(AccountCreateRequest request);
        Task<AccountDetailResponse> UpdateAccount(Guid accountId, AccountUpdateRequest request);
        Task<AccountDetailResponse> UpdateAccountProfile(Guid accountId, ProfileUpdateRequest request);
        Task<ProfileInfoResponse?> GetAccountProfileByGuid(Guid accountId, Guid? currentId);
        Task<ProfileInfoResponse?> GetAccountProfileByUsername(string username, Guid? currentId);
        Task<AccountProfilePreviewModel?> GetAccountProfilePreview(Guid targetId, Guid? currentId);
        Task ReactivateAccountAsync(Guid accountId);
    }
}
