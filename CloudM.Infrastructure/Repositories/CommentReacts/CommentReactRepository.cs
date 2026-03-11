using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Helpers;
using CloudM.Infrastructure.Models;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.CommentReacts
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
            return await _context.CommentReacts
                .Where(cr =>
                    cr.CommentId == commentId &&
                    cr.Account.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(cr.Account.RoleId))
                .CountAsync();
        }
        public async Task AddCommentReact(CommentReact commentReact)
        {
            await _context.CommentReacts.AddAsync(commentReact);
        }
        public Task RemoveCommentReact(CommentReact commentReact)
        {
            _context.CommentReacts.Remove(commentReact);
            return Task.CompletedTask;
        }
        public async Task<int> GetReactCountByCommentId(Guid commentId)
        {
            return await _context.CommentReacts.CountAsync(cr =>
                cr.CommentId == commentId &&
                cr.Account.Status == AccountStatusEnum.Active &&
                SocialRoleRules.SocialEligibleRoleIds.Contains(cr.Account.RoleId));
        }
        public async Task<CommentReact?> GetUserReactOnCommentAsync(Guid commentId, Guid accountId)
        {
            return await _context.CommentReacts
                .FirstOrDefaultAsync(cr =>
                    cr.CommentId == commentId &&
                    cr.AccountId == accountId &&
                    cr.Account.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(cr.Account.RoleId));
        }
        public async Task<bool> IsCurrentUserReactedOnCommentAsync(Guid commentId, Guid? currentId)
        {
            if (currentId == null)
                return false;
            return await _context.CommentReacts
                .AnyAsync(cr =>
                    cr.CommentId == commentId &&
                    cr.AccountId == currentId &&
                    cr.Account.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(cr.Account.RoleId));
        }
        public async Task<(List<AccountReactListModel> reacts, int totalItems)> GetAccountsReactOnCommentPaged(Guid commentId, Guid? currentId, int page, int pageSize)
        {
            var baseQuery = _context.CommentReacts
                .Where(r =>
                    r.CommentId == commentId &&
                    ((r.Account.Status == AccountStatusEnum.Active &&
                      SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)) ||
                     (currentId.HasValue && r.AccountId == currentId.Value)))
                .Select(r => new
                {
                    r.AccountId,
                    r.Account.Username,
                    r.Account.FullName,
                    r.Account.AvatarUrl,
                    r.ReactType,
                    r.CreatedAt,
                    IsFollowing = currentId.HasValue && _context.Follows.Any(f => f.FollowerId == currentId.Value && f.FollowedId == r.AccountId),
                    IsFollowRequested = currentId.HasValue && _context.FollowRequests.Any(fr => fr.RequesterId == currentId.Value && fr.TargetId == r.AccountId),
                    IsFollower = currentId.HasValue && _context.Follows.Any(f => f.FollowerId == r.AccountId && f.FollowedId == currentId.Value)
                });

            var totalItems = await baseQuery.CountAsync();

            if (currentId.HasValue)
            {
                var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId.Value);
                baseQuery = baseQuery.Where(x => !hiddenAccountIds.Contains(x.AccountId));
            }

            var reacts = await baseQuery
                .OrderByDescending(x => currentId.HasValue && x.AccountId == currentId.Value)
                .ThenByDescending(x => x.IsFollowing)
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
                    IsFollowRequested = x.IsFollowRequested,
                    IsFollower = x.IsFollower
                })
                .ToListAsync();

            return (reacts, totalItems);
        }
    }
}
