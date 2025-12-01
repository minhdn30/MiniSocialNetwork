using AutoMapper;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.Interfaces;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        public AccountService(IAccountRepository accountRepository, IMapper mapper)
        {
            _accountRepository = accountRepository;
            _mapper = mapper;
        }
        public async Task<ActionResult<PagedResponse<AccountOverviewResponse>>> GetAccountsAsync([FromQuery] AccountPagingRequest request)
        {
            var (accounts, totalItems) = await _accountRepository.GetAccountsAsync(request.Id, request.Username, request.Email, request.Fullname, request.Phone,
                request.RoleId, request.Gender, request.Status, request.IsEmailVerified, request.Page, request.PageSize);
            var mappedAccounts = _mapper.Map<IEnumerable<AccountOverviewResponse>>(accounts);
            var rs = new PagedResponse<AccountOverviewResponse>
            {
                Items = mappedAccounts,
                TotalItems = totalItems,
                Page = request.Page,
                PageSize = request.PageSize
            };
            return rs;
        }
        public async Task<ActionResult<AccountDetailResponse?>> GetAccountByGuid(Guid accountId)
        {
            var account = await _accountRepository.GetAccountById(accountId);
            if(account == null)
            {
                throw new NotFoundException($"Account with ID {accountId} not found.");
            }
            var mappedAccount = _mapper.Map<AccountDetailResponse>(account);
            return mappedAccount;
        }
        public async Task<AccountDetailResponse> CreateAccount([FromBody] AccountCreateRequest request)
        {
            var usernameExists = await _accountRepository.IsUsernameExist(request.Username);
            if (usernameExists)
            {
                throw new BadRequestException("Username already exists.");
            }
            var emailExists = await _accountRepository.IsEmailExist(request.Email);
            if (emailExists)
            {
                throw new BadRequestException("Email already exists.");
            }
            if(!Enum.IsDefined(typeof(RoleEnum), request.RoleId))
            {
                throw new BadRequestException("Role not found.");
            }
            var account = _mapper.Map<Account>(request);
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            await _accountRepository.AddAccount(account);
            var accountMapped = _mapper.Map<AccountDetailResponse>(account);
            return accountMapped;
        }
        public async Task<AccountDetailResponse> UpdateAccount(Guid accountId, [FromBody] AccountUpdateRequest request)
        {
            if (request.RoleId.HasValue && !Enum.IsDefined(typeof(RoleEnum), request.RoleId.Value))
            {
                throw new BadRequestException("Role not found.");
            }

            var account = await _accountRepository.GetAccountById(accountId);
            if(account  == null)
            {
                throw new NotFoundException($"Account with ID {accountId} not found.");
            }

            _mapper.Map(request, account);
            account.UpdatedAt = DateTime.UtcNow;
            await _accountRepository.UpdateAccount(account);
            return _mapper.Map<AccountDetailResponse>(account);
        }
    }
}
