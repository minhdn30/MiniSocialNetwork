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
        public async Task<(List<AccountReactListModel> reacts, int totalItems)> GetAccountsReactOnPostPaged(Guid postId, int page, int pageSize)
        {
            var query = _context.PostReacts
                .Where(r => r.PostId == postId)
                .Include(r => r.Account);

            var totalItems = await query.CountAsync();

            var reacts = await query
                .OrderBy(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new AccountReactListModel
                {
                    AccountId = r.AccountId,
                    Username = r.Account.Username,
                    FullName = r.Account.FullName,
                    AvatarUrl = r.Account.AvatarUrl,
                    ReactType = r.ReactType
                })
                .ToListAsync();

            return (reacts, totalItems);
        }

    }
}
