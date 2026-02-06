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
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.AccountSettingRepos;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.Posts;
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
        private readonly IAccountSettingRepository _accountSettingRepository;
        private readonly IMapper _mapper;
        private readonly ICloudinaryService _cloudinary;
        private readonly IFollowRepository _followRepository;
        private readonly IPostRepository _postRepository;

        public AccountService(
            IAccountRepository accountRepository, 
            IAccountSettingRepository accountSettingRepository,
            IMapper mapper, 
            ICloudinaryService cloudinary, 
            IFollowRepository followRepository, 
            IPostRepository postRepository)
        {
            _accountRepository = accountRepository;
            _accountSettingRepository = accountSettingRepository;
            _mapper = mapper;
            _cloudinary = cloudinary;
            _followRepository = followRepository;
            _postRepository = postRepository;
        }
        public async Task<ActionResult<PagedResponse<AccountOverviewResponse>>> GetAccountsAsync(AccountPagingRequest request)
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
        public async Task<ActionResult<AccountInfoResponse?>> GetAccountByGuid(Guid accountId)
        {
            var account = await _accountRepository.GetAccountById(accountId);
            if(account == null)
            {
                throw new NotFoundException($"Account with ID {accountId} not found.");
            }
            var followers = await _followRepository.CountFollowersAsync(accountId);
            var following = await _followRepository.CountFollowingAsync(accountId);
            var totalPosts = await _postRepository.CountPostsByAccountIdAsync(accountId);
            var result = new AccountInfoResponse
            {
                AccountInfo = _mapper.Map<AccountDetailResponse>(account),
                FollowInfo = new FollowCountResponse
                {
                    Followers = followers,
                    Following = following,
                    IsFollowedByCurrentUser = false
                },
                TotalPosts = totalPosts
            };
            return result;
        }
        public async Task<AccountDetailResponse> CreateAccount(AccountCreateRequest request)
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
        public async Task<AccountDetailResponse> UpdateAccount(Guid accountId, AccountUpdateRequest request)
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
        public async Task<AccountDetailResponse> UpdateAccountProfile(Guid accountId, ProfileUpdateRequest request)
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
                        await _cloudinary.DeleteMediaAsync(publicId, MediaTypeEnum.Image);
                    }
                }
                var imageURL = await _cloudinary.UploadImageAsync(request.Image);
                if (string.IsNullOrEmpty(imageURL))
                {
                    throw new InternalServerException("Image upload failed.");
                }
                account.AvatarUrl = imageURL;
                
            }
            if (request.CoverImage != null)
            {
                if (!string.IsNullOrEmpty(account.CoverUrl))
                {
                    var publicId = _cloudinary.GetPublicIdFromUrl(account.CoverUrl);
                    if (!string.IsNullOrEmpty(publicId))
                    {
                        await _cloudinary.DeleteMediaAsync(publicId, MediaTypeEnum.Image);
                    }
                }
                var coverURL = await _cloudinary.UploadImageAsync(request.CoverImage);
                if (string.IsNullOrEmpty(coverURL))
                {
                    throw new InternalServerException("Cover image upload failed.");
                }
                account.CoverUrl = coverURL;
            }

            // Map standard profile fields
            _mapper.Map(request, account);
            
            account.UpdatedAt = DateTime.UtcNow;
            await _accountRepository.UpdateAccount(account);
            return _mapper.Map<AccountDetailResponse>(account);
        }
        public async Task<ProfileInfoResponse?> GetAccountProfileByGuid(Guid accountId, Guid? currentId)
        {
            var profileModel = await _accountRepository.GetProfileInfoAsync(accountId, currentId);
            
            if (profileModel == null)
            {
                throw new NotFoundException($"Account with ID {accountId} not found or inactive.");
            }

            var result = new ProfileInfoResponse
            {
                AccountInfo = new ProfileDetailResponse
                {
                    AccountId = profileModel.AccountId,
                    Username = profileModel.Username,
                    Email = profileModel.Email,
                    FullName = profileModel.FullName,
                    AvatarUrl = profileModel.AvatarUrl,
                    Phone = profileModel.Phone,
                    Bio = profileModel.Bio,
                    CoverUrl = profileModel.CoverUrl,
                    Gender = profileModel.Gender,
                    Address = profileModel.Address,
                    CreatedAt = profileModel.CreatedAt
                },
                FollowInfo = new FollowCountResponse
                {
                    Followers = profileModel.FollowerCount,
                    Following = profileModel.FollowingCount,
                    IsFollowedByCurrentUser = profileModel.IsFollowedByCurrentUser
                },
                TotalPosts = profileModel.PostCount,
                IsCurrentUser = profileModel.IsCurrentUser
            };
            
            // Map the privacy settings into a DTO
            var currentSettings = new AccountSettingsResponse
            {
                EmailPrivacy = profileModel.EmailPrivacy,
                PhonePrivacy = profileModel.PhonePrivacy,
                AddressPrivacy = profileModel.AddressPrivacy,
                DefaultPostPrivacy = profileModel.DefaultPostPrivacy,
                FollowerPrivacy = profileModel.FollowerPrivacy,
                FollowingPrivacy = profileModel.FollowingPrivacy
            };

            // Enforce privacy logic
            if (!result.IsCurrentUser)
            {
                // Check Email Privacy
                if (!IsDataVisible(profileModel.EmailPrivacy, profileModel.IsFollowedByCurrentUser))
                {
                    result.AccountInfo.Email = null;
                }

                // Check Phone Privacy
                if (!IsDataVisible(profileModel.PhonePrivacy, profileModel.IsFollowedByCurrentUser))
                {
                    result.AccountInfo.Phone = null;
                }

                // Check Address Privacy
                if (!IsDataVisible(profileModel.AddressPrivacy, profileModel.IsFollowedByCurrentUser))
                {
                    result.AccountInfo.Address = null;
                }
            }
            else
            {
                // Only return settings values if it's the current user viewing their own profile
                result.Settings = currentSettings;
            }

            return result;
        }

        private bool IsDataVisible(AccountPrivacyEnum privacy, bool isFollowed)
        {
            return privacy == AccountPrivacyEnum.Public || 
                   (privacy == AccountPrivacyEnum.FollowOnly && isFollowed);
        }

        private string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return email;
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            var name = parts[0];
            if (name.Length <= 2) return name[0] + "***" + "@" + parts[1];
            return name.Substring(0, 2) + "***" + name.Substring(name.Length - 1) + "@" + parts[1];
        }
        public async Task<AccountProfilePreviewModel?> GetAccountProfilePreview(Guid targetId, Guid? currentId)
        {
            return await _accountRepository.GetProfilePreviewAsync(targetId, currentId);
        }

        public async Task ReactivateAccountAsync(Guid accountId)
        {
            var account = await _accountRepository.GetAccountById(accountId);
            if (account == null)
            {
                throw new NotFoundException($"Account with ID {accountId} not found.");
            }

            if (account.Status == AccountStatusEnum.Inactive)
            {
                account.Status = AccountStatusEnum.Active;
                account.UpdatedAt = DateTime.UtcNow;
                await _accountRepository.UpdateAccount(account);
            }
        }
    }
}
