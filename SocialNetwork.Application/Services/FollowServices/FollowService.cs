using AutoMapper;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.FollowDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.AccountSettingRepos;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Application.Services.RealtimeServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.FollowServices
{
    public class FollowService : IFollowService
    {
        private readonly IFollowRepository _followRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IAccountSettingRepository _accountSettingRepository;
        private readonly IMapper _mapper;
        private readonly IRealtimeService _realtimeService;
        private readonly IUnitOfWork _unitOfWork;

        public FollowService(IFollowRepository followRepository, IMapper mapper, IAccountRepository accountRepository, 
            IAccountSettingRepository accountSettingRepository, IRealtimeService realtimeService, IUnitOfWork unitOfWork)
        {
            _followRepository = followRepository;
            _mapper = mapper;
            _accountRepository = accountRepository;
            _accountSettingRepository = accountSettingRepository;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
        }

        public async Task<FollowCountResponse> FollowAsync(Guid followerId, Guid targetId)
        {
            // Use the optimized IsAccountIdExist which already checks for existence and Active status
            if (!await _accountRepository.IsAccountIdExist(targetId))
                throw new BadRequestException("This user is unavailable or does not exist.");

            if (!await _accountRepository.IsAccountIdExist(followerId))
                throw new ForbiddenException("You must reactivate your account to follow users.");

            // Check if record exists (regardless of status) to prevent duplicate key exception (500)
            var recordExists = await _followRepository.IsFollowRecordExistAsync(followerId, targetId);
            if (recordExists)
                throw new BadRequestException("You already follow this user.");

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _followRepository.AddFollowAsync(new Follow { FollowerId = followerId, FollowedId = targetId });
                
                // Commit changes first so Count queries see the new data
                await _unitOfWork.CommitAsync();

                var targetCounts = await _followRepository.GetFollowCountsAsync(targetId);
                var myCounts = await _followRepository.GetFollowCountsAsync(followerId);

                // Send realtime notification
                await _realtimeService.NotifyFollowChangedAsync(
                    followerId, 
                    targetId, 
                    "follow", 
                    targetCounts.Followers, 
                    targetCounts.Following,
                    myCounts.Followers,
                    myCounts.Following
                );

                return new FollowCountResponse
                {
                    Followers = targetCounts.Followers,
                    Following = targetCounts.Following,
                    IsFollowedByCurrentUser = true
                };
            });
        }
        public async Task<FollowCountResponse> UnfollowAsync(Guid followerId, Guid targetId)
        {
            // Allowed to unfollow even if target is inactive, as long as the record exists
            var recordExists = await _followRepository.IsFollowRecordExistAsync(followerId, targetId);
            if (!recordExists)
                throw new BadRequestException("You are not following this user.");

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _followRepository.RemoveFollowAsync(followerId, targetId);

                // Commit changes first
                await _unitOfWork.CommitAsync();

                var targetCounts = await _followRepository.GetFollowCountsAsync(targetId);
                var myCounts = await _followRepository.GetFollowCountsAsync(followerId);

                // Send realtime notification
                await _realtimeService.NotifyFollowChangedAsync(
                    followerId, 
                    targetId, 
                    "unfollow", 
                    targetCounts.Followers, 
                    targetCounts.Following,
                    myCounts.Followers,
                    myCounts.Following
                );

                return new FollowCountResponse
                {
                    Followers = targetCounts.Followers,
                    Following = targetCounts.Following,
                    IsFollowedByCurrentUser = false
                };
            });
        }

        public Task<bool> IsFollowingAsync(Guid followerId, Guid targetId)
        {
            return _followRepository.IsFollowingAsync(followerId, targetId);
        }
        public async Task<PagedResponse<AccountWithFollowStatusModel>> GetFollowersAsync(Guid accountId, Guid? currentId, FollowPagingRequest request)
        {
            if (!await _accountRepository.IsAccountIdExist(accountId))
                throw new NotFoundException($"Account with ID {accountId} does not exist.");

            // Privacy Check
            if (currentId != accountId)
            {
                var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(accountId);
                var privacy = settings != null ? settings.FollowerPrivacy : AccountPrivacyEnum.Public;

                if (privacy == AccountPrivacyEnum.Private)
                    throw new ForbiddenException("This user's followers list is private.");

                if (privacy == AccountPrivacyEnum.FollowOnly)
                {
                    bool isFollowing = currentId.HasValue && await _followRepository.IsFollowingAsync(currentId.Value, accountId);
                    if (!isFollowing)
                        throw new ForbiddenException("You must follow this user to see their followers list.");
                }
            }

            var (items, total) = await _followRepository.GetFollowersAsync(accountId, currentId, request.Keyword, request.SortByCreatedASC, request.Page, request.PageSize);

            return new PagedResponse<AccountWithFollowStatusModel>
            {
                Items = items,
                TotalItems = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        public async Task<PagedResponse<AccountWithFollowStatusModel>> GetFollowingAsync(Guid accountId, Guid? currentId, FollowPagingRequest request)
        {
            if (!await _accountRepository.IsAccountIdExist(accountId))
                throw new NotFoundException($"Account with ID {accountId} does not exist.");

            // Privacy Check
            if (currentId != accountId)
            {
                var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(accountId);
                var privacy = settings != null ? settings.FollowingPrivacy : AccountPrivacyEnum.Public;

                if (privacy == AccountPrivacyEnum.Private)
                    throw new ForbiddenException("This user's following list is private.");

                if (privacy == AccountPrivacyEnum.FollowOnly)
                {
                    bool isFollowing = currentId.HasValue && await _followRepository.IsFollowingAsync(currentId.Value, accountId);
                    if (!isFollowing)
                        throw new ForbiddenException("You must follow this user to see their following list.");
                }
            }

            var (items, total) = await _followRepository.GetFollowingAsync(accountId, currentId, request.Keyword, request.SortByCreatedASC, request.Page, request.PageSize);
            return new PagedResponse<AccountWithFollowStatusModel>
            {
                Items = items,
                TotalItems = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        
        public async Task<FollowCountResponse> GetStatsAsync(Guid userId)
        {
             var counts = await _followRepository.GetFollowCountsAsync(userId);
             return new FollowCountResponse
             {
                 Followers = counts.Followers,
                 Following = counts.Following
             };
        }

    }
}
