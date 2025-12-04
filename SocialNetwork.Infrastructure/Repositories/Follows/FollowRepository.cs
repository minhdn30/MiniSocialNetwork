using Microsoft.EntityFrameworkCore;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Domain.Entities;
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
                .AnyAsync(f => f.FollowerId == followerId && f.FollowedId == followedId);
        }

        public async Task AddFollowAsync(Follow follow)
        {
            _context.Follows.Add(follow);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveFollowAsync(Guid followerId, Guid followedId)
        {
            var follow = await _context.Follows
                .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowedId == followedId);

            if (follow != null)
            {
                _context.Follows.Remove(follow);
                await _context.SaveChangesAsync();
            }
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
        public async Task<(List<AccountFollowListModel> Items, int TotalItems)> GetFollowersAsync(Guid accountId, string? keyword, int page, int pageSize)
        {
            var query = _context.Follows
                .Where(f => f.FollowedId == accountId)
                .Select(f => new
                {
                    FollowRecord = f,
                    FollowerAccount = f.Follower
                })
                .AsQueryable();

            //search by FullName or Username
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var lower = keyword.Trim().ToLower();
                query = query.Where(x =>
                    (x.FollowerAccount.FullName != null &&
                     x.FollowerAccount.FullName.ToLower().Contains(lower))
                    ||
                    (x.FollowerAccount.Username != null &&
                     x.FollowerAccount.Username.ToLower().Contains(lower))
                );
            }

            int totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.FollowRecord.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AccountFollowListModel
                {
                    AccountId = x.FollowerAccount.AccountId,
                    Username = x.FollowerAccount.Username,
                    AvatarUrl = x.FollowerAccount.AvatarUrl,
                    FullName = x.FollowerAccount.FullName,
                })
                .ToListAsync();

            return (items, totalItems);
        }

        //get a list of people you follow
        public async Task<(List<AccountFollowListModel> Items, int TotalItems)> GetFollowingAsync(Guid accountId, string? keyword, int page, int pageSize)
        {
            var query = _context.Follows
                .Where(f => f.FollowerId == accountId)
                .Select(f => new
                {
                    FollowRecord = f,
                    FollowedAccount = f.Followed
                })
                .AsQueryable();
            //search by FullName or Username
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var lower = keyword.Trim().ToLower();
                query = query.Where(x =>
                    (x.FollowedAccount.FullName != null &&
                     x.FollowedAccount.FullName.ToLower().Contains(lower))
                    ||
                    (x.FollowedAccount.Username != null &&
                     x.FollowedAccount.Username.ToLower().Contains(lower))
                );
            }

            int totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.FollowRecord.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AccountFollowListModel
                {
                    AccountId = x.FollowedAccount.AccountId,
                    Username = x.FollowedAccount.Username,
                    AvatarUrl = x.FollowedAccount.AvatarUrl,
                    FullName = x.FollowedAccount.FullName
                })
                .ToListAsync();

            return (items, totalItems);
        }
        public async Task<int> CountFollowersAsync(Guid accountId)
        {
            return await _context.Follows
                .Where(f => f.FollowedId == accountId)
                .CountAsync();
        }
        public async Task<int> CountFollowingAsync(Guid accountId)
        {
            return await _context.Follows
                .Where(f => f.FollowerId == accountId)
                .CountAsync();
        }


    }
}
