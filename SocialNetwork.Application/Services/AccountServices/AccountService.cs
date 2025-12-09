using AutoMapper;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.FollowDTOs;
using SocialNetwork.Application.Services.CloudinaryServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.Follows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.AccountServices
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        private readonly ICloudinaryService _cloudinary;
        private readonly IFollowRepository _followRepository;
        public AccountService(IAccountRepository accountRepository, IMapper mapper, ICloudinaryService cloudinary, IFollowRepository followRepository)
        {
            _accountRepository = accountRepository;
            _mapper = mapper;
            _cloudinary = cloudinary;
            _followRepository = followRepository;
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
        public async Task<ActionResult<ProfileResponse?>> GetAccountByGuid(Guid accountId)
        {
            var account = await _accountRepository.GetAccountById(accountId);
            if(account == null)
            {
                throw new NotFoundException($"Account with ID {accountId} not found.");
            }
            var followers = await _followRepository.CountFollowersAsync(accountId);
            var following = await _followRepository.CountFollowingAsync(accountId);
            //Guid? currentUserId = _currentUser.GetUserId();
            //bool isFollowedByCurrentUser = false;

            //if (currentUserId.HasValue)
            //{
            //    isFollowedByCurrentUser =
            //        await _followRepository.IsFollowingAsync(currentUserId.Value, accountId);
            //}
            var result = new ProfileResponse
            {
                AccountInfo = _mapper.Map<AccountDetailResponse>(account),
                FollowInfo = new FollowCountResponse
                {
                    Followers = followers,
                    Following = following,
                    //IsFollowedByCurrentUser = isFollowedByCurrentUser
                }
            };
            return result;
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
            return _mapper.Map<AccountDetailResponse>(account);
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
        public async Task<AccountDetailResponse> UpdateAccountProfile(Guid accountId, [FromBody] ProfileUpdateRequest request)
        {
            var account = await _accountRepository.GetAccountById(accountId);
            if (account == null)
            {
                throw new NotFoundException($"Account with ID {accountId} not found.");
            }
            if (request.Image != null)
            {
                if (!string.IsNullOrEmpty(account.AvatarUrl))
                {
                    var publicId = _cloudinary.GetPublicIdFromUrl(account.AvatarUrl);
                    if (!string.IsNullOrEmpty(publicId))
                    {
                        await _cloudinary.DeleteMediaAsync(publicId);
                    }
                }
                var imageURL = await _cloudinary.UploadImageAsync(request.Image);
                if (string.IsNullOrEmpty(imageURL))
                {
                    throw new InternalServerException("Image upload failed.");
                }
                account.AvatarUrl = imageURL;
                
            }
            _mapper.Map(request, account);
            account.UpdatedAt = DateTime.UtcNow;
            await _accountRepository.UpdateAccount(account);
            return _mapper.Map<AccountDetailResponse>(account);
        }

    }
}
