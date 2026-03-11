using Microsoft.EntityFrameworkCore;
using CloudM.Infrastructure.Models;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

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
                .AnyAsync(f =>
                    f.FollowerId == followerId &&
                    f.FollowedId == followedId &&
                    f.Followed.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Followed.RoleId));
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

        public async Task<bool> AddFollowIgnoreExistingAsync(Follow follow, CancellationToken cancellationToken = default)
        {
            if (follow == null ||
                follow.FollowerId == Guid.Empty ||
                follow.FollowedId == Guid.Empty ||
                follow.FollowerId == follow.FollowedId)
            {
                return false;
            }

            var followerIdParam = new NpgsqlParameter<Guid>("p_follower_id", follow.FollowerId);
            var followedIdParam = new NpgsqlParameter<Guid>("p_followed_id", follow.FollowedId);
            var createdAtParam = new NpgsqlParameter<DateTime>("p_created_at", NpgsqlDbType.TimestampTz)
            {
                TypedValue = DateTime.SpecifyKind(
                    follow.CreatedAt == default ? DateTime.UtcNow : follow.CreatedAt,
                    DateTimeKind.Utc)
            };

            var affected = await _context.Database.ExecuteSqlRawAsync(@"
INSERT INTO ""Follows"" (""FollowerId"", ""FollowedId"", ""CreatedAt"")
VALUES (@p_follower_id, @p_followed_id, @p_created_at)
ON CONFLICT (""FollowerId"", ""FollowedId"") DO NOTHING;",
                new object[] { followerIdParam, followedIdParam, createdAtParam },
                cancellationToken);

            return affected > 0;
        }

        public async Task<int> RemoveFollowAsync(Guid followerId, Guid followedId)
        {
            return await _context.Follows
                .Where(f => f.FollowerId == followerId && f.FollowedId == followedId)
                .ExecuteDeleteAsync();
        }

        public async Task<List<Guid>> GetFollowingIdsAsync(Guid followerId)
        {
            return await _context.Follows
                .Where(f =>
                    f.FollowerId == followerId &&
                    f.Followed.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Followed.RoleId))
                .Select(f => f.FollowedId)
                .ToListAsync();
        }

        public async Task<List<Guid>> GetFollowerIdsAsync(Guid followedId)
        {
            return await _context.Follows
                .Where(f =>
                    f.FollowedId == followedId &&
                    f.Follower.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId))
                .Select(f => f.FollowerId)
                .ToListAsync();
        }

        public async Task<HashSet<Guid>> GetFollowerIdsInTargetsAsync(Guid followedId, IEnumerable<Guid> targetIds)
        {
            var normalizedTargetIds = (targetIds ?? Enumerable.Empty<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedTargetIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            var followerIds = await _context.Follows
                .Where(f =>
                    f.FollowedId == followedId &&
                    normalizedTargetIds.Contains(f.FollowerId) &&
                    f.Follower.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId))
                .Select(f => f.FollowerId)
                .Distinct()
                .ToListAsync();

            return followerIds.ToHashSet();
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
                    (
                        f.FollowerId == currentId &&
                        normalizedTargetIds.Contains(f.FollowedId) &&
                        f.Followed.Status == AccountStatusEnum.Active &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(f.Followed.RoleId)
                    ) ||
                    (
                        f.FollowedId == currentId &&
                        normalizedTargetIds.Contains(f.FollowerId) &&
                        f.Follower.Status == AccountStatusEnum.Active &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId)
                    ))
                .Select(f => f.FollowerId == currentId ? f.FollowedId : f.FollowerId)
                .Distinct()
                .ToListAsync();

            return connectedIds.ToHashSet();
        }

        //get a list of your followers
        public async Task<(List<AccountWithFollowStatusModel> Items, int TotalItems)> GetFollowersAsync(Guid accountId, Guid? currentId, string? keyword, bool? sortByCreatedASC, int page, int pageSize)
        {
            var query = _context.Follows
                .Where(f =>
                    f.FollowedId == accountId &&
                    f.Follower.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId))
                .Select(f => new
                {
                    f.Follower.AccountId,
                    f.Follower.Username,
                    f.Follower.FullName,
                    f.Follower.AvatarUrl,
                    f.CreatedAt,
                    IsFollowing = currentId.HasValue && _context.Follows.Any(fol => fol.FollowerId == currentId.Value && fol.FollowedId == f.FollowerId),
                    IsFollowRequested = currentId.HasValue && _context.FollowRequests.Any(fr => fr.RequesterId == currentId.Value && fr.TargetId == f.FollowerId),
                    IsFollower = currentId.HasValue && _context.Follows.Any(fol => fol.FollowerId == f.FollowerId && fol.FollowedId == currentId.Value)
                });

            if (currentId.HasValue)
            {
                var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId.Value);
                query = query.Where(x => !hiddenAccountIds.Contains(x.AccountId));
            }

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
                    IsFollowRequested = x.IsFollowRequested,
                    IsFollower = x.IsFollower
                })
                .ToListAsync();

            return (items, totalItems);
        }

        //get a list of people you follow
        public async Task<(List<AccountWithFollowStatusModel> Items, int TotalItems)> GetFollowingAsync(Guid accountId, Guid? currentId, string? keyword, bool? sortByCreatedASC, int page, int pageSize)
        {
            var query = _context.Follows
                .Where(f =>
                    f.FollowerId == accountId &&
                    f.Followed.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Followed.RoleId))
                .Select(f => new
                {
                    f.Followed.AccountId,
                    f.Followed.Username,
                    f.Followed.FullName,
                    f.Followed.AvatarUrl,
                    f.CreatedAt,
                    IsFollowing = currentId.HasValue && _context.Follows.Any(fol => fol.FollowerId == currentId.Value && fol.FollowedId == f.FollowedId),
                    IsFollowRequested = currentId.HasValue && _context.FollowRequests.Any(fr => fr.RequesterId == currentId.Value && fr.TargetId == f.FollowedId),
                    IsFollower = currentId.HasValue && _context.Follows.Any(fol => fol.FollowerId == f.FollowedId && fol.FollowedId == currentId.Value)
                });

            if (currentId.HasValue)
            {
                var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId.Value);
                query = query.Where(x => !hiddenAccountIds.Contains(x.AccountId));
            }

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
                    IsFollowRequested = x.IsFollowRequested,
                    IsFollower = x.IsFollower
                })
                .ToListAsync();

            return (items, totalItems);
        }
        public async Task<int> CountFollowersAsync(Guid accountId)
        {
            return await _context.Follows
                .Where(f =>
                    f.FollowedId == accountId &&
                    f.Follower.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId))
                .CountAsync();
        }
        public async Task<int> CountFollowingAsync(Guid accountId)
        {
            return await _context.Follows
                .Where(f =>
                    f.FollowerId == accountId &&
                    f.Followed.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Followed.RoleId))
                .CountAsync();
        }

        public async Task<(int Followers, int Following)> GetFollowCountsAsync(Guid targetId)
        {
            // Count using separate queries to ensure stable SQL translation and correct active status filtering
            var followers = await _context.Follows
                .CountAsync(f =>
                    f.FollowedId == targetId &&
                    f.Follower.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId));

            var following = await _context.Follows
                .CountAsync(f =>
                    f.FollowerId == targetId &&
                    f.Followed.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Followed.RoleId));

            return (followers, following);
        }

        public async Task<Dictionary<Guid, (int Followers, int Following)>> GetFollowCountsByAccountIdsAsync(IEnumerable<Guid> accountIds, CancellationToken cancellationToken = default)
        {
            var safeAccountIds = (accountIds ?? Enumerable.Empty<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();
            if (safeAccountIds.Count == 0)
            {
                return new Dictionary<Guid, (int Followers, int Following)>();
            }

            var followerCounts = await _context.Follows
                .Where(f =>
                    safeAccountIds.Contains(f.FollowedId) &&
                    f.Follower.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId))
                .GroupBy(f => f.FollowedId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.AccountId, x => x.Count, cancellationToken);

            var followingCounts = await _context.Follows
                .Where(f =>
                    safeAccountIds.Contains(f.FollowerId) &&
                    f.Followed.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Followed.RoleId))
                .GroupBy(f => f.FollowerId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.AccountId, x => x.Count, cancellationToken);

            return safeAccountIds.ToDictionary(
                accountId => accountId,
                accountId => (
                    Followers: followerCounts.GetValueOrDefault(accountId, 0),
                    Following: followingCounts.GetValueOrDefault(accountId, 0)));
        }

        public async Task<List<InsertedFollowRelation>> AddFollowsIgnoreExistingAsync(IEnumerable<Follow> follows, CancellationToken cancellationToken = default)
        {
            var safeFollows = (follows ?? Enumerable.Empty<Follow>())
                .Where(x => x.FollowerId != Guid.Empty && x.FollowedId != Guid.Empty && x.FollowerId != x.FollowedId)
                .GroupBy(x => new { x.FollowerId, x.FollowedId })
                .Select(x => x.OrderBy(item => item.CreatedAt).First())
                .ToList();
            if (safeFollows.Count == 0)
            {
                return new List<InsertedFollowRelation>();
            }

            var followerIdsParam = new NpgsqlParameter<Guid[]>("p_follower_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            {
                TypedValue = safeFollows.Select(x => x.FollowerId).ToArray()
            };
            var followedIdsParam = new NpgsqlParameter<Guid[]>("p_followed_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            {
                TypedValue = safeFollows.Select(x => x.FollowedId).ToArray()
            };
            var createdAtParam = new NpgsqlParameter<DateTime[]>("p_created_ats", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz)
            {
                TypedValue = safeFollows
                    .Select(x => DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc))
                    .ToArray()
            };

            return await _context.Database
                .SqlQueryRaw<InsertedFollowRelation>(@"
INSERT INTO ""Follows"" (""FollowerId"", ""FollowedId"", ""CreatedAt"")
SELECT source.""FollowerId"", source.""FollowedId"", source.""CreatedAt""
FROM unnest(@p_follower_ids, @p_followed_ids, @p_created_ats) AS source(""FollowerId"", ""FollowedId"", ""CreatedAt"")
ON CONFLICT (""FollowerId"", ""FollowedId"") DO NOTHING
RETURNING ""FollowerId"", ""FollowedId"";",
                    new object[] { followerIdsParam, followedIdsParam, createdAtParam })
                .ToListAsync(cancellationToken);
        }


    }
}
