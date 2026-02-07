using AutoMapper;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.AccountSettingDTOs;
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
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
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
        private readonly IUnitOfWork _unitOfWork;

        public AccountService(
            IAccountRepository accountRepository, 
            IAccountSettingRepository accountSettingRepository,
            IMapper mapper, 
            ICloudinaryService cloudinary, 
            IFollowRepository followRepository, 
            IPostRepository postRepository,
            IUnitOfWork unitOfWork)
        {
            _accountRepository = accountRepository;
            _accountSettingRepository = accountSettingRepository;
            _mapper = mapper;
            _cloudinary = cloudinary;
            _followRepository = followRepository;
            _postRepository = postRepository;
            _unitOfWork = unitOfWork;
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
            await _unitOfWork.CommitAsync();
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
            await _unitOfWork.CommitAsync();
            return _mapper.Map<AccountDetailResponse>(account);
        }
        public async Task<AccountDetailResponse> UpdateAccountProfile(Guid accountId, ProfileUpdateRequest request)
        {
            // 1. Fetch account early - No lock needed here
            var account = await _accountRepository.GetAccountById(accountId);
            if (account == null)
            {
                throw new NotFoundException($"Account with ID {accountId} not found.");
            }

            string? oldAvatarPublicId = null;
            string? oldCoverPublicId = null;
            string? newAvatarUrl = null;
            string? newCoverUrl = null;

            // 2. Prepare Cloudinary Upload Tasks - Execute OUTSIDE transaction for performance
            var uploadTasks = new List<Task>();

            if (request.DeleteAvatar == true)
            {
                if (!string.IsNullOrEmpty(account.AvatarUrl))
                    oldAvatarPublicId = _cloudinary.GetPublicIdFromUrl(account.AvatarUrl);
            }
            else if (request.AvatarFile != null)
            {
                if (!string.IsNullOrEmpty(account.AvatarUrl))
                    oldAvatarPublicId = _cloudinary.GetPublicIdFromUrl(account.AvatarUrl);

                uploadTasks.Add(Task.Run(async () => {
                    newAvatarUrl = await _cloudinary.UploadImageAsync(request.AvatarFile);
                }));
            }

            if (request.DeleteCover == true)
            {
                if (!string.IsNullOrEmpty(account.CoverUrl))
                    oldCoverPublicId = _cloudinary.GetPublicIdFromUrl(account.CoverUrl);
            }
            else if (request.CoverFile != null)
            {
                if (!string.IsNullOrEmpty(account.CoverUrl))
                    oldCoverPublicId = _cloudinary.GetPublicIdFromUrl(account.CoverUrl);

                uploadTasks.Add(Task.Run(async () => {
                    newCoverUrl = await _cloudinary.UploadImageAsync(request.CoverFile);
                }));
            }

            // Sync point: Parallel uploads take the most time (network latency)
            if (uploadTasks.Any())
            {
                await Task.WhenAll(uploadTasks);
                
                // Verify results
                if (request.AvatarFile != null && string.IsNullOrEmpty(newAvatarUrl))
                    throw new InternalServerException("Avatar upload failed.");
                if (request.CoverFile != null && string.IsNullOrEmpty(newCoverUrl))
                    throw new InternalServerException("Cover image upload failed.");
            }

            // 3. Start DB Transaction - Keep it minimal
            var result = await _unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    // Assign new URLs
                    if (request.DeleteAvatar == true) account.AvatarUrl = null;
                    else if (newAvatarUrl != null) account.AvatarUrl = newAvatarUrl;

                    if (request.DeleteCover == true) account.CoverUrl = null;
                    else if (newCoverUrl != null) account.CoverUrl = newCoverUrl;

                    // Normalize fields
                    if (string.IsNullOrWhiteSpace(request.Bio)) request.Bio = null;
                    if (string.IsNullOrWhiteSpace(request.Phone)) request.Phone = null;
                    if (string.IsNullOrWhiteSpace(request.Address)) request.Address = null;

                    // Bulk map standard fields
                    _mapper.Map(request, account);
                    
                    // Re-enforce Urls just in case Mapper tried to set them
                    if (request.DeleteAvatar == true) account.AvatarUrl = null;
                    else if (newAvatarUrl != null) account.AvatarUrl = newAvatarUrl;
                    
                    if (request.DeleteCover == true) account.CoverUrl = null;
                    else if (newCoverUrl != null) account.CoverUrl = newCoverUrl;

                    account.UpdatedAt = DateTime.UtcNow;
                    await _accountRepository.UpdateAccount(account);

                    return _mapper.Map<AccountDetailResponse>(account);
                },
                // Rollback cleanup for NEW images
                async () =>
                {
                    var orphanTasks = new List<Task>();
                    if (!string.IsNullOrEmpty(newAvatarUrl))
                    {
                        var pid = _cloudinary.GetPublicIdFromUrl(newAvatarUrl);
                        if (!string.IsNullOrEmpty(pid)) orphanTasks.Add(_cloudinary.DeleteMediaAsync(pid, MediaTypeEnum.Image));
                    }
                    if (!string.IsNullOrEmpty(newCoverUrl))
                    {
                        var pid = _cloudinary.GetPublicIdFromUrl(newCoverUrl);
                        if (!string.IsNullOrEmpty(pid)) orphanTasks.Add(_cloudinary.DeleteMediaAsync(pid, MediaTypeEnum.Image));
                    }
                    if (orphanTasks.Any()) await Task.WhenAll(orphanTasks);
                }
            );

            // 4. Post-Commit: Cleanup OLD images
            var cleanupTasks = new List<Task>();
            if (!string.IsNullOrEmpty(oldAvatarPublicId))
                cleanupTasks.Add(_cloudinary.DeleteMediaAsync(oldAvatarPublicId, MediaTypeEnum.Image));
            if (!string.IsNullOrEmpty(oldCoverPublicId))
                cleanupTasks.Add(_cloudinary.DeleteMediaAsync(oldCoverPublicId, MediaTypeEnum.Image));
            
            if (cleanupTasks.Any()) await Task.WhenAll(cleanupTasks);

            return result;
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
                PhonePrivacy = profileModel.PhonePrivacy,
                AddressPrivacy = profileModel.AddressPrivacy,
                DefaultPostPrivacy = profileModel.DefaultPostPrivacy,
                FollowerPrivacy = profileModel.FollowerPrivacy,
                FollowingPrivacy = profileModel.FollowingPrivacy
            };

            // Enforce privacy logic
            if (!result.IsCurrentUser)
            {
                // Always hide Email
                result.AccountInfo.Email = null;

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

        public async Task<ProfileInfoResponse?> GetAccountProfileByUsername(string username, Guid? currentId)
        {
            var profileModel = await _accountRepository.GetProfileInfoByUsernameAsync(username, currentId);
            
            if (profileModel == null)
            {
                throw new NotFoundException($"Account with username '{username}' not found or inactive.");
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
                PhonePrivacy = profileModel.PhonePrivacy,
                AddressPrivacy = profileModel.AddressPrivacy,
                DefaultPostPrivacy = profileModel.DefaultPostPrivacy,
                FollowerPrivacy = profileModel.FollowerPrivacy,
                FollowingPrivacy = profileModel.FollowingPrivacy
            };

            // Enforce privacy logic
            if (!result.IsCurrentUser)
            {
                // Always hide Email
                result.AccountInfo.Email = null;

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
            else if (account.Status == AccountStatusEnum.Active)
            {
                throw new BadRequestException("Account is already active.");
            }
        }
    }
}
