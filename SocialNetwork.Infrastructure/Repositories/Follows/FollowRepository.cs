using Microsoft.EntityFrameworkCore;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Follows
{
    public class FollowRepository : IFollowRepository
    {
        private readonly AppDbContext _context;
        public FollowRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<bool> IsFollowingAsync(Guid followerId, Guid followedId)
        {
            return await _context.Follows
                .AnyAsync(f => f.FollowerId == followerId && f.FollowedId == followedId && f.Followed.Status == AccountStatusEnum.Active);
        }

        public async Task<bool> IsFollowRecordExistAsync(Guid followerId, Guid followedId)
        {
            return await _context.Follows
                .AnyAsync(f => f.FollowerId == followerId && f.FollowedId == followedId);
        }

        public async Task AddFollowAsync(Follow follow)
        {
            _context.Follows.Add(follow);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveFollowAsync(Guid followerId, Guid followedId)
        {
            await _context.Follows
                .Where(f => f.FollowerId == followerId && f.FollowedId == followedId)
                .ExecuteDeleteAsync();
        }

        public async Task<List<Guid>> GetFollowingIdsAsync(Guid followerId)
        {
            return await _context.Follows
                .Where(f => f.FollowerId == followerId)
                .Select(f => f.FollowedId)
                .ToListAsync();
        }

        public async Task<List<Guid>> GetFollowerIdsAsync(Guid followedId)
        {
            return await _context.Follows
                .Where(f => f.FollowedId == followedId)
                .Select(f => f.FollowerId)
                .ToListAsync();
        }
        //get a list of your followers
        public async Task<(List<AccountWithFollowStatusModel> Items, int TotalItems)> GetFollowersAsync(Guid accountId, Guid? currentId, string? keyword, int page, int pageSize)
        {
            var query = _context.Follows
                .Where(f => f.FollowedId == accountId && f.Follower.Status == AccountStatusEnum.Active)
                .Select(f => new
                {
                    FollowRecord = f,
                    FollowerAccount = f.Follower
                })
                .AsQueryable();

            //search by FullName using Trigram search (ILike)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var searchKeyword = $"%{keyword.Trim()}%";
                query = query.Where(x => EF.Functions.ILike(x.FollowerAccount.FullName, searchKeyword));
            }

            int totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.FollowRecord.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AccountWithFollowStatusModel
                {
                    AccountId = x.FollowerAccount.AccountId,
                    Username = x.FollowerAccount.Username,
                    AvatarUrl = x.FollowerAccount.AvatarUrl,
                    FullName = x.FollowerAccount.FullName,
                    IsFollowing = currentId.HasValue && _context.Follows.Any(f => f.FollowerId == currentId.Value && f.FollowedId == x.FollowerAccount.AccountId),
                    IsFollower = currentId.HasValue && _context.Follows.Any(f => f.FollowerId == x.FollowerAccount.AccountId && f.FollowedId == currentId.Value)
                })
                .ToListAsync();

            return (items, totalItems);
        }

        //get a list of people you follow
        public async Task<(List<AccountWithFollowStatusModel> Items, int TotalItems)> GetFollowingAsync(Guid accountId, Guid? currentId, string? keyword, int page, int pageSize)
        {
            var query = _context.Follows
                .Where(f => f.FollowerId == accountId && f.Followed.Status == AccountStatusEnum.Active)
                .Select(f => new
                {
                    FollowRecord = f,
                    FollowedAccount = f.Followed
                })
                .AsQueryable();

            //search by FullName using Trigram search (ILike)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var searchKeyword = $"%{keyword.Trim()}%";
                query = query.Where(x => EF.Functions.ILike(x.FollowedAccount.FullName, searchKeyword));
            }

            int totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.FollowRecord.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AccountWithFollowStatusModel
                {
                    AccountId = x.FollowedAccount.AccountId,
                    Username = x.FollowedAccount.Username,
                    AvatarUrl = x.FollowedAccount.AvatarUrl,
                    FullName = x.FollowedAccount.FullName,
                    IsFollowing = currentId.HasValue && _context.Follows.Any(f => f.FollowerId == currentId.Value && f.FollowedId == x.FollowedAccount.AccountId),
                    IsFollower = currentId.HasValue && _context.Follows.Any(f => f.FollowerId == x.FollowedAccount.AccountId && f.FollowedId == currentId.Value)
                })
                .ToListAsync();

            return (items, totalItems);
        }
        public async Task<int> CountFollowersAsync(Guid accountId)
        {
            return await _context.Follows
                .Where(f => f.FollowedId == accountId && f.Follower.Status == AccountStatusEnum.Active)
                .CountAsync();
        }
        public async Task<int> CountFollowingAsync(Guid accountId)
        {
            return await _context.Follows
                .Where(f => f.FollowerId == accountId && f.Followed.Status == AccountStatusEnum.Active)
                .CountAsync();
        }

        public async Task<(int Followers, int Following)> GetFollowCountsAsync(Guid targetId)
        {
            // Count using separate queries to ensure stable SQL translation and correct active status filtering
            var followers = await _context.Follows
                .CountAsync(f => f.FollowedId == targetId && f.Follower.Status == AccountStatusEnum.Active);

            var following = await _context.Follows
                .CountAsync(f => f.FollowerId == targetId && f.Followed.Status == AccountStatusEnum.Active);

            return (followers, following);
        }


    }
}
