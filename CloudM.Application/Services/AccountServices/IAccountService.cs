using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.AccountServices
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
