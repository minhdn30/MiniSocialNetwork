using Microsoft.EntityFrameworkCore;
using CloudM.Infrastructure.Models;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.Follows
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

        public Task AddFollowAsync(Follow follow)
        {
            _context.Follows.Add(follow);
            return Task.CompletedTask;
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

        public async Task<HashSet<Guid>> GetConnectedAccountIdsAsync(Guid currentId, IEnumerable<Guid> targetIds)
        {
            var normalizedTargetIds = targetIds
                .Where(id => id != Guid.Empty && id != currentId)
                .Distinct()
                .ToList();

            if (normalizedTargetIds.Count == 0)
                return new HashSet<Guid>();

            var connectedIds = await _context.Follows
                .Where(f =>
                    (f.FollowerId == currentId && normalizedTargetIds.Contains(f.FollowedId)) ||
                    (f.FollowedId == currentId && normalizedTargetIds.Contains(f.FollowerId)))
                .Select(f => f.FollowerId == currentId ? f.FollowedId : f.FollowerId)
                .Distinct()
                .ToListAsync();

            return connectedIds.ToHashSet();
        }

        //get a list of your followers
        public async Task<(List<AccountWithFollowStatusModel> Items, int TotalItems)> GetFollowersAsync(Guid accountId, Guid? currentId, string? keyword, bool? sortByCreatedASC, int page, int pageSize)
        {
            var query = _context.Follows
                .Where(f => f.FollowedId == accountId && f.Follower.Status == AccountStatusEnum.Active)
                .Select(f => new
                {
                    f.Follower.AccountId,
                    f.Follower.Username,
                    f.Follower.FullName,
                    f.Follower.AvatarUrl,
                    f.CreatedAt,
                    IsFollowing = currentId.HasValue && _context.Follows.Any(fol => fol.FollowerId == currentId.Value && fol.FollowedId == f.FollowerId),
                    IsFollower = currentId.HasValue && _context.Follows.Any(fol => fol.FollowerId == f.FollowerId && fol.FollowedId == currentId.Value)
                });

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var words = keyword.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    var searchPattern = $"%{word}%";
                    query = query.Where(x => EF.Functions.ILike(AppDbContext.Unaccent(x.FullName), AppDbContext.Unaccent(searchPattern)) 
                                          || EF.Functions.ILike(x.Username, searchPattern));
                }

            }

            int totalItems = await query.CountAsync();

            var sortedQuery = sortByCreatedASC.HasValue
                ? (sortByCreatedASC.Value 
                    ? query.OrderBy(x => x.CreatedAt) 
                    : query.OrderByDescending(x => x.CreatedAt))
                : query.OrderByDescending(x => currentId.HasValue && x.AccountId == currentId.Value)
                       .ThenByDescending(x => x.IsFollowing)
                       .ThenByDescending(x => x.IsFollower)
                       .ThenByDescending(x => x.CreatedAt);

            var items = await sortedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AccountWithFollowStatusModel
                {
                    AccountId = x.AccountId,
                    Username = x.Username,
                    AvatarUrl = x.AvatarUrl,
                    FullName = x.FullName,
                    IsFollowing = x.IsFollowing,
                    IsFollower = x.IsFollower
                })
                .ToListAsync();

            return (items, totalItems);
        }

        //get a list of people you follow
        public async Task<(List<AccountWithFollowStatusModel> Items, int TotalItems)> GetFollowingAsync(Guid accountId, Guid? currentId, string? keyword, bool? sortByCreatedASC, int page, int pageSize)
        {
            var query = _context.Follows
                .Where(f => f.FollowerId == accountId && f.Followed.Status == AccountStatusEnum.Active)
                .Select(f => new
                {
                    f.Followed.AccountId,
                    f.Followed.Username,
                    f.Followed.FullName,
                    f.Followed.AvatarUrl,
                    f.CreatedAt,
                    IsFollowing = currentId.HasValue && _context.Follows.Any(fol => fol.FollowerId == currentId.Value && fol.FollowedId == f.FollowedId),
                    IsFollower = currentId.HasValue && _context.Follows.Any(fol => fol.FollowerId == f.FollowedId && fol.FollowedId == currentId.Value)
                });

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var words = keyword.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    var searchPattern = $"%{word}%";
                    query = query.Where(x => EF.Functions.ILike(AppDbContext.Unaccent(x.FullName), AppDbContext.Unaccent(searchPattern)) 
                                          || EF.Functions.ILike(x.Username, searchPattern));
                }

            }

            int totalItems = await query.CountAsync();

            var sortedQuery = sortByCreatedASC.HasValue
                ? (sortByCreatedASC.Value 
                    ? query.OrderBy(x => x.CreatedAt) 
                    : query.OrderByDescending(x => x.CreatedAt))
                : query.OrderByDescending(x => currentId.HasValue && x.AccountId == currentId.Value)
                       .ThenByDescending(x => x.IsFollowing)
                       .ThenByDescending(x => x.IsFollower)
                       .ThenByDescending(x => x.CreatedAt);

            var items = await sortedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AccountWithFollowStatusModel
                {
                    AccountId = x.AccountId,
                    Username = x.Username,
                    AvatarUrl = x.AvatarUrl,
                    FullName = x.FullName,
                    IsFollowing = x.IsFollowing,
                    IsFollower = x.IsFollower
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
