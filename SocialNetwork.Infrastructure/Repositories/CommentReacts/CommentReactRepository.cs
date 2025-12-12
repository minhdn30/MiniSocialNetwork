using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.CommentReacts
{
    public class CommentReactRepository : ICommentReactRepository
    {
        private readonly AppDbContext _context;
        public CommentReactRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<int> CountCommentReactAsync (Guid commentId)
        {
            return await _context.CommentReacts.Where(cr => cr.CommentId == commentId).CountAsync();
        }
        public async Task AddCommentReact(CommentReact commentReact)
        {
            await _context.CommentReacts.AddAsync(commentReact);
            await _context.SaveChangesAsync();
        }
        public async Task RemoveCommentReact(CommentReact commentReact)
        {
            _context.CommentReacts.Remove(commentReact);
            await _context.SaveChangesAsync();
        }
        public async Task<int> GetReactCountByCommentId(Guid commentId)
        {
            return await _context.CommentReacts.CountAsync(cr => cr.CommentId == commentId);
        }
        public async Task<CommentReact?> GetUserReactOnCommentAsync(Guid commentId, Guid accountId)
        {
            return await _context.CommentReacts
                .FirstOrDefaultAsync(cr => cr.CommentId == commentId && cr.AccountId == accountId);
        }
        public async Task<bool> IsCurrentUserReactedOnCommentAsync(Guid commentId, Guid? currentId)
        {
            if (currentId == null)
                return false;
            return await _context.CommentReacts
                .AnyAsync(cr => cr.CommentId == commentId && cr.AccountId == currentId);
        }
        public async Task<(List<AccountReactListModel> reacts, int totalItems)> GetAccountsReactOnCommentPaged(Guid commentId, int page, int pageSize)
        {
            var query = _context.CommentReacts
                .Where(r => r.CommentId == commentId)
                .Include(r => r.Account);
            var totalItems = await query.CountAsync();
            var reacts = await query
                .OrderBy(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new AccountReactListModel
                {
                    AccountId = r.AccountId,
                    FullName = r.Account.FullName,
                    Username = r.Account.Username,
                    AvatarUrl = r.Account.AvatarUrl,
                    ReactType = r.ReactType
                })
                .ToListAsync();
            return (reacts, totalItems);
        }
    }
}
