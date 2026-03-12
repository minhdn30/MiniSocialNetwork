using AutoMapper;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.AccountSettingDTOs;
using CloudM.Application.DTOs.AuthDTOs;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.FollowDTOs;
using CloudM.Application.DTOs.SearchDTOs;
using CloudM.Application.Helpers.CloudinaryHelpers;
using CloudM.Application.Helpers.StoryHelpers;
using CloudM.Application.Services.AuthServices;
using CloudM.Infrastructure.Services.Cloudinary;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Application.Services.StoryViewServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.AccountSettingRepos;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.AccountServices
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
        private readonly IRealtimeService _realtimeService;
        private readonly IStoryRingStateHelper _storyRingStateHelper;

        public AccountService(
            IAccountRepository accountRepository, 
            IAccountSettingRepository accountSettingRepository,
            IMapper mapper, 
            ICloudinaryService cloudinary, 
            IFollowRepository followRepository, 
            IPostRepository postRepository,
            IUnitOfWork unitOfWork,
            IRealtimeService realtimeService,
            IStoryViewService? storyViewService = null,
            IStoryRingStateHelper? storyRingStateHelper = null)
        {
            _accountRepository = accountRepository;
            _accountSettingRepository = accountSettingRepository;
            _mapper = mapper;
            _cloudinary = cloudinary;
            _followRepository = followRepository;
            _postRepository = postRepository;
            _unitOfWork = unitOfWork;
            _realtimeService = realtimeService;
            _storyRingStateHelper = storyRingStateHelper ?? new StoryRingStateHelper(storyViewService);
        }
        public async Task<ActionResult<PagedResponse<AccountOverviewResponse>>> GetAccountsAsync(AccountPagingRequest request)
        {
            var (accounts, totalItems) = await _accountRepository.GetAccountsAsync(request.Id, request.Username, request.Email, request.Fullname, request.Phone,
                request.RoleId, request.Gender, request.Status, request.Page, request.PageSize);
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
            var normalizedUsername = (request.Username ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();

            var usernameExists = await _accountRepository.IsUsernameExist(normalizedUsername);
            if (usernameExists)
            {
                throw new BadRequestException("Username already exists.");
            }
            var emailExists = await _accountRepository.IsEmailExist(normalizedEmail);
            if (emailExists)
            {
                throw new BadRequestException("Email already exists.");
            }

            var account = _mapper.Map<Account>(request);
            account.Username = normalizedUsername;
            account.Email = normalizedEmail;
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            account.Settings ??= new AccountSettings
            {
                AccountId = account.AccountId
            };
            await _accountRepository.AddAccount(account);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<AccountDetailResponse>(account);
        }
        public async Task<AccountDetailResponse> UpdateAccount(Guid accountId, AccountUpdateRequest request)
        {
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
            // fetch account early
            var account = await _accountRepository.GetAccountById(accountId);
            if (account == null)
            {
                throw new NotFoundException($"Account with ID {accountId} not found.");
            }

            string? oldAvatarUrl = null;
            string? oldCoverUrl = null;
            string? newAvatarUrl = null;
            string? newCoverUrl = null;
            var cleanupPlan = new CloudinaryCleanupPlan(_cloudinary);

            // prepare cloudinary upload tasks
            var uploadTasks = new List<Task>();

            if (request.DeleteAvatar == true)
            {
                if (!string.IsNullOrEmpty(account.AvatarUrl))
                    oldAvatarUrl = account.AvatarUrl;
            }
            else if (request.AvatarFile != null)
            {
                if (!string.IsNullOrEmpty(account.AvatarUrl))
                    oldAvatarUrl = account.AvatarUrl;

                uploadTasks.Add(Task.Run(async () => {
                    newAvatarUrl = await _cloudinary.UploadImageAsync(request.AvatarFile);
                    cleanupPlan.AddRollbackDeleteByUrl(newAvatarUrl, MediaTypeEnum.Image);
                }));
            }

            if (request.DeleteCover == true)
            {
                if (!string.IsNullOrEmpty(account.CoverUrl))
                    oldCoverUrl = account.CoverUrl;
            }
            else if (request.CoverFile != null)
            {
                if (!string.IsNullOrEmpty(account.CoverUrl))
                    oldCoverUrl = account.CoverUrl;

                uploadTasks.Add(Task.Run(async () => {
                    newCoverUrl = await _cloudinary.UploadImageAsync(request.CoverFile);
                    cleanupPlan.AddRollbackDeleteByUrl(newCoverUrl, MediaTypeEnum.Image);
                }));
            }

            // sync point
            if (uploadTasks.Any())
            {
                try
                {
                    await Task.WhenAll(uploadTasks);

                    // verify results
                    if (request.AvatarFile != null && string.IsNullOrEmpty(newAvatarUrl))
                        throw new InternalServerException("Avatar upload failed.");
                    if (request.CoverFile != null && string.IsNullOrEmpty(newCoverUrl))
                        throw new InternalServerException("Cover image upload failed.");
                }
                catch
                {
                    await cleanupPlan.ExecuteRollbackAsync();
                    throw;
                }
            }

            // start db transaction
            var result = await _unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    // assign new urls
                    if (request.DeleteAvatar == true) account.AvatarUrl = null;
                    else if (newAvatarUrl != null) account.AvatarUrl = newAvatarUrl;

                    if (request.DeleteCover == true) account.CoverUrl = null;
                    else if (newCoverUrl != null) account.CoverUrl = newCoverUrl;

                    // normalize fields
                    if (string.IsNullOrWhiteSpace(request.Bio)) request.Bio = null;
                    if (string.IsNullOrWhiteSpace(request.Phone)) request.Phone = null;
                    if (string.IsNullOrWhiteSpace(request.Address)) request.Address = null;

                    // bulk map standard fields
                    _mapper.Map(request, account);
                    
                    // re-enforce urls
                    if (request.DeleteAvatar == true) account.AvatarUrl = null;
                    else if (newAvatarUrl != null) account.AvatarUrl = newAvatarUrl;
                    
                    if (request.DeleteCover == true) account.CoverUrl = null;
                    else if (newCoverUrl != null) account.CoverUrl = newCoverUrl;

                    account.UpdatedAt = DateTime.UtcNow;
                    await _accountRepository.UpdateAccount(account);

                    // update privacy settings if provided
                    if (request.PhonePrivacy.HasValue || request.AddressPrivacy.HasValue)
                    {
                        var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(accountId);
                        bool isNewSettings = false;

                        if (settings == null)
                        {
                            settings = new AccountSettings { AccountId = accountId };
                            isNewSettings = true;
                        }

                        if (request.PhonePrivacy.HasValue)
                        {
                            settings.PhonePrivacy = request.PhonePrivacy.Value;
                        }

                        if (request.AddressPrivacy.HasValue)
                        {
                            settings.AddressPrivacy = request.AddressPrivacy.Value;
                        }

                        if (isNewSettings)
                        {
                            await _accountSettingRepository.AddAccountSettingsAsync(settings);
                        }
                        else
                        {
                            await _accountSettingRepository.UpdateAccountSettingsAsync(settings);
                        }
                    }

                    return _mapper.Map<AccountDetailResponse>(account);
                },
                // rollback cleanup for new images
                cleanupPlan.ExecuteRollbackAsync
            );

            // post commit cleanup old images
            cleanupPlan.AddPostCommitDeleteByUrl(oldAvatarUrl, MediaTypeEnum.Image);
            cleanupPlan.AddPostCommitDeleteByUrl(oldCoverUrl, MediaTypeEnum.Image);
            await cleanupPlan.ExecutePostCommitAsync();
            
            // real-time notification
            _ = _realtimeService.NotifyProfileUpdatedAsync(accountId, result);

            return result;
        }
        public async Task<ProfileInfoResponse?> GetAccountProfileByGuid(Guid accountId, Guid? currentId)
        {
            var profileModel = await _accountRepository.GetProfileInfoAsync(accountId, currentId);
            
            if (profileModel == null)
            {
                throw new NotFoundException($"Account with ID {accountId} not found or inactive.");
            }

            var storyRingState = await ResolveStoryRingStateAsync(profileModel.AccountId, currentId);

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
                    CreatedAt = profileModel.CreatedAt,
                    StoryRingState = storyRingState
                },
                FollowInfo = new FollowCountResponse
                {
                    Followers = profileModel.FollowerCount,
                    Following = profileModel.FollowingCount,
                    IsFollowedByCurrentUser = profileModel.IsFollowedByCurrentUser,
                    IsFollowRequestPendingByCurrentUser = profileModel.IsFollowRequestPendingByCurrentUser,
                    RelationStatus = ResolveFollowRelationStatus(
                        profileModel.IsFollowedByCurrentUser,
                        profileModel.IsFollowRequestPendingByCurrentUser),
                    TargetFollowPrivacy = profileModel.FollowPrivacy
                },
                TotalPosts = profileModel.PostCount,
                IsCurrentUser = profileModel.IsCurrentUser
            };
            
            // map the privacy settings into a dto
            var currentSettings = new AccountSettingsResponse
            {
                PhonePrivacy = profileModel.PhonePrivacy,
                AddressPrivacy = profileModel.AddressPrivacy,
                DefaultPostPrivacy = profileModel.DefaultPostPrivacy,
                FollowerPrivacy = profileModel.FollowerPrivacy,
                FollowingPrivacy = profileModel.FollowingPrivacy,
                FollowPrivacy = profileModel.FollowPrivacy,
                StoryHighlightPrivacy = profileModel.StoryHighlightPrivacy,
                GroupChatInvitePermission = profileModel.GroupChatInvitePermission,
                OnlineStatusVisibility = profileModel.OnlineStatusVisibility,
                TagPermission = profileModel.TagPermission
            };

            // enforce privacy logic
            if (!result.IsCurrentUser)
            {
                // always hide email
                result.AccountInfo.Email = null;

                // check phone privacy
                if (!IsDataVisible(profileModel.PhonePrivacy, profileModel.IsFollowedByCurrentUser))
                {
                    result.AccountInfo.Phone = null;
                }

                // check address privacy
                if (!IsDataVisible(profileModel.AddressPrivacy, profileModel.IsFollowedByCurrentUser))
                {
                    result.AccountInfo.Address = null;
                }
            }
            else
            {
                // only return settings values if it's the current user viewing their own profile
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

            var storyRingState = await ResolveStoryRingStateAsync(profileModel.AccountId, currentId);

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
                    CreatedAt = profileModel.CreatedAt,
                    StoryRingState = storyRingState
                },
                FollowInfo = new FollowCountResponse
                {
                    Followers = profileModel.FollowerCount,
                    Following = profileModel.FollowingCount,
                    IsFollowedByCurrentUser = profileModel.IsFollowedByCurrentUser,
                    IsFollowRequestPendingByCurrentUser = profileModel.IsFollowRequestPendingByCurrentUser,
                    RelationStatus = ResolveFollowRelationStatus(
                        profileModel.IsFollowedByCurrentUser,
                        profileModel.IsFollowRequestPendingByCurrentUser),
                    TargetFollowPrivacy = profileModel.FollowPrivacy
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
                FollowingPrivacy = profileModel.FollowingPrivacy,
                FollowPrivacy = profileModel.FollowPrivacy,
                StoryHighlightPrivacy = profileModel.StoryHighlightPrivacy,
                GroupChatInvitePermission = profileModel.GroupChatInvitePermission,
                OnlineStatusVisibility = profileModel.OnlineStatusVisibility,
                TagPermission = profileModel.TagPermission
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

        private static FollowRelationStatusEnum ResolveFollowRelationStatus(bool isFollowing, bool isFollowRequestPending)
        {
            if (isFollowing)
            {
                return FollowRelationStatusEnum.Following;
            }

            if (isFollowRequestPending)
            {
                return FollowRelationStatusEnum.Requested;
            }

            return FollowRelationStatusEnum.None;
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
            var result = await _accountRepository.GetProfilePreviewAsync(targetId, currentId);
            if (result == null)
            {
                return null;
            }

            result.Account.StoryRingState = await ResolveStoryRingStateAsync(result.Account.AccountId, currentId);
            return result;
        }

        public async Task<List<SidebarAccountSearchResponse>> SearchSidebarAccountsAsync(
            Guid currentId,
            string keyword,
            int limit = 20)
        {
            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            if (normalizedKeyword.Length == 0)
            {
                return new List<SidebarAccountSearchResponse>();
            }

            var results = await _accountRepository.SearchSidebarAccountsAsync(
                currentId,
                normalizedKeyword,
                limit);

            return results.Select(MapSidebarAccountSearchResponse).ToList();
        }

        public async Task<List<PostTagAccountSearchResponse>> SearchAccountsForPostTagAsync(
            Guid currentId,
            Guid? visibilityOwnerId,
            string keyword,
            PostPrivacyEnum? postPrivacy,
            IEnumerable<Guid>? excludeAccountIds,
            int limit = 10)
        {
            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            if (normalizedKeyword.Length == 0)
            {
                return new List<PostTagAccountSearchResponse>();
            }

            var results = await _accountRepository.SearchAccountsForPostTagAsync(
                currentId,
                visibilityOwnerId,
                normalizedKeyword,
                postPrivacy,
                excludeAccountIds,
                limit);

            return results.Select(x => new PostTagAccountSearchResponse
            {
                AccountId = x.AccountId,
                Username = x.Username,
                FullName = x.FullName,
                AvatarUrl = x.AvatarUrl,
                IsFollowing = x.IsFollowing,
                IsFollower = x.IsFollower,
                LastContactedAt = x.LastContactedAt,
                MatchScore = x.MatchScore,
                FollowingScore = x.FollowingScore,
                FollowerScore = x.FollowerScore,
                RecentChatScore = x.RecentChatScore,
                TotalScore = x.TotalScore
            }).ToList();
        }

        private static SidebarAccountSearchResponse MapSidebarAccountSearchResponse(
            SidebarAccountSearchModel item)
        {
            return new SidebarAccountSearchResponse
            {
                AccountId = item.AccountId,
                Username = item.Username,
                FullName = item.FullName,
                AvatarUrl = item.AvatarUrl,
                IsFollowing = item.IsFollowing,
                IsFollower = item.IsFollower,
                HasDirectConversation = item.HasDirectConversation,
                LastContactedAt = item.LastContactedAt,
                LastSearchedAt = item.LastSearchedAt
            };
        }

        private async Task<StoryRingStateEnum> ResolveStoryRingStateAsync(Guid targetId, Guid? currentId)
        {
            return await _storyRingStateHelper.ResolveAsync(currentId, targetId);
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
                await _unitOfWork.CommitAsync();
            }
            else if (account.Status == AccountStatusEnum.Active)
            {
                throw new BadRequestException("Account is already active.");
            }
        }
    }
}
