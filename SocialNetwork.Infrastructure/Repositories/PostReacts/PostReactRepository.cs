using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.PostReacts
{
    public class PostReactRepository : IPostReactRepository
    {
        private readonly AppDbContext _context;
        public PostReactRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task AddPostReact(PostReact postReact)
        {
            await _context.PostReacts.AddAsync(postReact);
            await _context.SaveChangesAsync();
        }
        public async Task RemovePostReact(PostReact postReact)
        {
            _context.PostReacts.Remove(postReact);
            await _context.SaveChangesAsync();
        }
        public async Task<int> GetReactCountByPostId(Guid postId)
        {
            return await _context.PostReacts.CountAsync(pr => pr.PostId == postId);
        }
        public async Task<PostReact?> GetUserReactOnPostAsync(Guid postId, Guid accountId)
        {
            return await _context.PostReacts
                .FirstOrDefaultAsync(pr => pr.PostId == postId && pr.AccountId == accountId);
        }
        public async Task<bool> IsCurrentUserReactedOnPostAsync(Guid postId, Guid? currentId)
        {
            if (currentId == null)
                return false;
            return await _context.PostReacts
                .AnyAsync(pr => pr.PostId == postId && pr.AccountId == currentId);
        }
        public async Task<(List<AccountReactListModel> reacts, int totalItems)> GetAccountsReactOnPostPaged(Guid postId, Guid? currentId, int page, int pageSize)
        {
            // First: Calculate flags once using projection
            var baseQuery = _context.PostReacts
                .Where(r => r.PostId == postId)
                .Select(r => new
                {
                    r.AccountId,
                    r.Account.Username,
                    r.Account.FullName,
                    r.Account.AvatarUrl,
                    r.ReactType,
                    r.CreatedAt,
                    IsFollowing = currentId.HasValue && _context.Follows.Any(f => f.FollowerId == currentId.Value && f.FollowedId == r.AccountId),
                    IsFollower = currentId.HasValue && _context.Follows.Any(f => f.FollowerId == r.AccountId && f.FollowedId == currentId.Value)
                });

            var totalItems = await baseQuery.CountAsync();

            var reacts = await baseQuery
                .OrderByDescending(x => x.IsFollowing)
                .ThenByDescending(x => x.IsFollower)
                .ThenBy(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AccountReactListModel
                {
                    AccountId = x.AccountId,
                    Username = x.Username,
                    FullName = x.FullName,
                    AvatarUrl = x.AvatarUrl,
                    ReactType = x.ReactType,
                    IsFollowing = x.IsFollowing,
                    IsFollower = x.IsFollower
                })
                .ToListAsync();

            return (reacts, totalItems);
        }

    }
}
