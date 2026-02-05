using AutoMapper;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.FollowDTOs;
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

namespace SocialNetwork.Application.Services.FollowServices
{
    public class FollowService : IFollowService
    {
        private readonly  IFollowRepository _followRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        public FollowService(IFollowRepository followRepository, IMapper mapper, IAccountRepository accountRepository)
        {
            _followRepository = followRepository;
            _mapper = mapper;
            _accountRepository = accountRepository;
        }
        public async Task<FollowCountResponse> FollowAsync(Guid followerId, Guid targetId)
        {
            if (followerId == targetId)
                throw new BadRequestException("You cannot follow yourself.");

            // Use the optimized IsAccountIdExist which already checks for existence and Active status
            if (!await _accountRepository.IsAccountIdExist(targetId))
                throw new BadRequestException("This user is unavailable or does not exist.");

            // Check if record exists (regardless of status) to prevent duplicate key exception (500)
            var recordExists = await _followRepository.IsFollowRecordExistAsync(followerId, targetId);
            
            if (!await _accountRepository.IsAccountIdExist(followerId))
                throw new ForbiddenException("You must reactivate your account to follow users.");
            
            if (recordExists)
                throw new BadRequestException("You already follow this user.");

            await _followRepository.AddFollowAsync(new Follow { FollowerId = followerId, FollowedId = targetId });

            var counts = await _followRepository.GetFollowCountsAsync(targetId);
            return new FollowCountResponse
            {
                Followers = counts.Followers,
                Following = counts.Following,
                IsFollowedByCurrentUser = true
            };
        }
        public async Task<FollowCountResponse> UnfollowAsync(Guid followerId, Guid targetId)
        {
            // Allowed to unfollow even if target is inactive, as long as the record exists
            var recordExists = await _followRepository.IsFollowRecordExistAsync(followerId, targetId);
            if (!recordExists)
                throw new BadRequestException("You are not following this user.");

            await _followRepository.RemoveFollowAsync(followerId, targetId);

            var counts = await _followRepository.GetFollowCountsAsync(targetId);
            return new FollowCountResponse
            {
                Followers = counts.Followers,
                Following = counts.Following,
                IsFollowedByCurrentUser = false
            };
        }
        public Task<bool> IsFollowingAsync(Guid followerId, Guid targetId)
        {
            return _followRepository.IsFollowingAsync(followerId, targetId);
        }
        public async Task<PagedResponse<AccountWithFollowStatusModel>> GetFollowersAsync(Guid accountId, Guid? currentId, FollowPagingRequest request)
        {
            if (!await _accountRepository.IsAccountIdExist(accountId))
                throw new NotFoundException($"Account with ID {accountId} does not exist.");
            var (items, total) = await _followRepository.GetFollowersAsync(accountId, currentId, request.Keyword, request.Page, request.PageSize);

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
            var (items, total) = await _followRepository.GetFollowingAsync(accountId, currentId, request.Keyword, request.Page, request.PageSize);
            return new PagedResponse<AccountWithFollowStatusModel>
            {
                Items = items,
                TotalItems = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

    }
}
