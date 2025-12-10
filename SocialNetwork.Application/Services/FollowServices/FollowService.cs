using AutoMapper;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.FollowDTOs;
using SocialNetwork.Domain.Entities;
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
        public async Task<int> FollowAsync(Guid followerId, Guid targetId)
        {
            if (followerId == targetId)
                throw new BadRequestException("You cannot follow yourself.");

            var exists = await _followRepository.IsFollowingAsync(followerId, targetId);
            if (exists)
                throw new BadRequestException("You already follow this user.");

            var follow = new Follow
            {
                FollowerId = followerId,
                FollowedId = targetId
            };

            await _followRepository.AddFollowAsync(follow);
            return await _followRepository.CountFollowersAsync(targetId);
        }
        public async Task<int> UnfollowAsync(Guid followerId, Guid targetId)
        {
            var exists = await _followRepository.IsFollowingAsync(followerId, targetId);
            if (!exists)
                throw new BadRequestException("You are not following this user.");

            await _followRepository.RemoveFollowAsync(followerId, targetId);
            return await _followRepository.CountFollowersAsync(targetId);
        }
        public Task<bool> IsFollowingAsync(Guid followerId, Guid targetId)
        {
            return _followRepository.IsFollowingAsync(followerId, targetId);
        }
        public async Task<PagedResponse<AccountFollowListModel>> GetFollowersAsync(Guid accountId, FollowPagingRequest request)
        {
            if (!await _accountRepository.IsAccountIdExist(accountId))
                throw new NotFoundException($"Account with ID {accountId} does not exist.");
            var (items, total) = await _followRepository.GetFollowersAsync(accountId, request.Keyword, request.Page, request.PageSize);

            return new PagedResponse<AccountFollowListModel>
            {
                Items = items,
                TotalItems = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        public async Task<PagedResponse<AccountFollowListModel>> GetFollowingAsync(Guid accountId, FollowPagingRequest request)
        {
            if(!await _accountRepository.IsAccountIdExist(accountId))
                throw new NotFoundException($"Account with ID {accountId} does not exist.");

            var (items, total) = await _followRepository.GetFollowingAsync(accountId, request.Keyword, request.Page, request.PageSize);

            return new PagedResponse<AccountFollowListModel>
            {
                Items = items,
                TotalItems = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
    }
}
