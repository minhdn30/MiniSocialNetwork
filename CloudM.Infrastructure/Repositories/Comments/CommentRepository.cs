using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.Comments
{
    public class CommentRepository : ICommentRepository
    {
        private readonly AppDbContext _context;
        public CommentRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<(IEnumerable<CommentWithReplyCountModel> items, int totalItems, DateTime? nextCursorCreatedAt, Guid? nextCursorCommentId)> GetCommentsByPostIdWithReplyCountAsync(Guid postId, Guid? currentId, DateTime? cursorCreatedAt, Guid? cursorCommentId, int pageSize, Guid? priorityCommentId = null)
        {
            if (pageSize <= 0) pageSize = 10;

            var baseQuery = _context.Comments
                .AsNoTracking()
                .Where(c =>
                    c.PostId == postId &&
                    c.ParentCommentId == null &&
                    c.Account.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId));

            var totalItems = await baseQuery.CountAsync();

            var postOwnerId = await _context.Posts
                .Where(p => p.PostId == postId)
                .Select(p => p.AccountId)
                .FirstOrDefaultAsync();

            var effectivePriorityCommentId = priorityCommentId.HasValue && priorityCommentId.Value != Guid.Empty
                ? priorityCommentId.Value
                : Guid.Empty;
            CommentWithReplyCountModel? priorityItem = null;
            if (effectivePriorityCommentId != Guid.Empty && !cursorCreatedAt.HasValue && !cursorCommentId.HasValue)
            {
                priorityItem = await baseQuery
                    .Where(c => c.CommentId == effectivePriorityCommentId)
                    .Select(c => new CommentWithReplyCountModel
                    {
                        CommentId = c.CommentId,
                        PostId = c.PostId,
                        Owner = new AccountBasicInfoModel
                        {
                            AccountId = c.Account.AccountId,
                            FullName = c.Account.FullName,
                            Username = c.Account.Username,
                            AvatarUrl = c.Account.AvatarUrl,
                            Status = c.Account.Status
                        },
                        Content = c.Content,
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt,
                        ReactCount = _context.CommentReacts.Count(r => r.CommentId == c.CommentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                        ReplyCount = _context.Comments.Count(r => r.ParentCommentId == c.CommentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                        IsCommentReactedByCurrentUser = currentId != null && _context.CommentReacts.Any(r => r.CommentId == c.CommentId && r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                        PostOwnerId = postOwnerId
                    })
                    .FirstOrDefaultAsync();
            }

            var query = baseQuery;

            if (priorityItem != null)
            {
                query = query.Where(c => c.CommentId != priorityItem.CommentId);
            }
            else if (effectivePriorityCommentId != Guid.Empty)
            {
                query = query.Where(c => c.CommentId != effectivePriorityCommentId);
            }

            if (cursorCreatedAt.HasValue && cursorCommentId.HasValue)
            {
                query = query.Where(c =>
                    c.CreatedAt < cursorCreatedAt.Value
                    || (c.CreatedAt == cursorCreatedAt.Value && c.CommentId.CompareTo(cursorCommentId.Value) < 0));
            }

            var rawItems = await query
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.CommentId)
                .Take(pageSize + 1)
                .Select(c => new CommentWithReplyCountModel
                {
                    CommentId = c.CommentId,
                    PostId = c.PostId,
                    Owner = new AccountBasicInfoModel
                    {
                        AccountId = c.Account.AccountId,
                        FullName = c.Account.FullName,
                        Username = c.Account.Username,
                        AvatarUrl = c.Account.AvatarUrl,
                        Status = c.Account.Status
                    },
                    Content = c.Content,
                    CreatedAt = c.CreatedAt,
                    ReactCount = _context.CommentReacts.Count(r => r.CommentId == c.CommentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    ReplyCount = _context.Comments.Count(r => r.ParentCommentId == c.CommentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    IsCommentReactedByCurrentUser = currentId != null && _context.CommentReacts.Any(r => r.CommentId == c.CommentId && r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    PostOwnerId = postOwnerId
                })
                .ToListAsync();

            var hasMore = rawItems.Count > pageSize;
            var pagedItems = hasMore
                ? rawItems.Take(pageSize).ToList()
                : rawItems;

            var items = priorityItem == null
                ? pagedItems
                : new[] { priorityItem }
                    .Concat(pagedItems)
                    .ToList();

            DateTime? nextCursorCreatedAt = null;
            Guid? nextCursorCommentId = null;
            if (hasMore && pagedItems.Count > 0)
            {
                var last = pagedItems[^1];
                nextCursorCreatedAt = last.CreatedAt;
                nextCursorCommentId = last.CommentId;
            }

            return (items, totalItems, nextCursorCreatedAt, nextCursorCommentId);
        }

        public async Task<Comment?> GetCommentById(Guid commentId)
        {
            return await _context.Comments.Include(c => c.Account).FirstOrDefaultAsync(c =>
                c.CommentId == commentId &&
                c.Account.Status == AccountStatusEnum.Active &&
                SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId));
        }
        public async Task AddComment(Comment comment)
        {
            await _context.Comments.AddAsync(comment);
        }
        public Task UpdateComment(Comment comment)
        {
            _context.Comments.Update(comment);
            return Task.CompletedTask;
        }
        public async Task<bool> IsCommentExist(Guid commentId)
        {
            return await _context.Comments.AnyAsync(c =>
                c.CommentId == commentId &&
                c.Account.Status == AccountStatusEnum.Active &&
                SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId));
        }
        public async Task<int> CountCommentsByPostId(Guid postId)
        {
            //comment (not reply)
            return await _context.Comments.CountAsync(c =>
                c.PostId == postId &&
                c.ParentCommentId == null &&
                c.Account.Status == AccountStatusEnum.Active &&
                SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId));
        }
        public async Task DeleteCommentWithReplies(Guid commentId)
        {
            //get comment and its replies
            var commentsToDelete = await _context.Comments
                .Where(c => c.CommentId == commentId || c.ParentCommentId == commentId)
                .ToListAsync();

            if (commentsToDelete.Count == 0)
                return;

            _context.Comments.RemoveRange(commentsToDelete);
        }

        public async Task<List<Comment>> GetCommentThreadForDeleteAsync(Guid commentId)
        {
            return await _context.Comments
                .Include(x => x.Account)
                .Where(x => x.CommentId == commentId || x.ParentCommentId == commentId)
                .ToListAsync();
        }

        public async Task<bool> IsCommentCanReply(Guid commentId)
        {
            return await _context.Comments.AnyAsync(c =>
                c.CommentId == commentId &&
                c.ParentCommentId == null &&
                c.Account.Status == AccountStatusEnum.Active &&
                SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId));
        }
        public async Task<int> CountCommentRepliesAsync(Guid commentId)
        {
            int count = 0;
            count += await _context.Comments.Where(c =>
                c.ParentCommentId == commentId &&
                c.Account.Status == AccountStatusEnum.Active &&
                SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId)).CountAsync();
            return count;
        }
        public async Task<(IEnumerable<ReplyCommentModel> items, int totalItems, DateTime? nextCursorCreatedAt, Guid? nextCursorCommentId)> GetRepliesByCommentIdAsync(Guid parentCommentId, Guid? currentId, DateTime? cursorCreatedAt, Guid? cursorCommentId, int pageSize, Guid? priorityReplyId = null)
        {
            if (pageSize <= 0) pageSize = 10;

            var baseQuery = _context.Comments
                .AsNoTracking()
                .Where(c =>
                    c.ParentCommentId == parentCommentId &&
                    c.Account.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId));

            var totalItems = await baseQuery.CountAsync();

            var query = baseQuery;

            if (cursorCreatedAt.HasValue && cursorCommentId.HasValue)
            {
                query = query.Where(c =>
                    c.CreatedAt > cursorCreatedAt.Value
                    || (c.CreatedAt == cursorCreatedAt.Value && c.CommentId.CompareTo(cursorCommentId.Value) > 0));
            }

            var postOwnerId = await _context.Comments
                .Where(c => c.CommentId == parentCommentId)
                .Select(c => c.Post.AccountId)
                .FirstOrDefaultAsync();

            var effectivePriorityReplyId = priorityReplyId.HasValue && priorityReplyId.Value != Guid.Empty
                ? priorityReplyId.Value
                : Guid.Empty;
            ReplyCommentModel? priorityItem = null;
            if (effectivePriorityReplyId != Guid.Empty && !cursorCreatedAt.HasValue && !cursorCommentId.HasValue)
            {
                priorityItem = await baseQuery
                    .Where(c => c.CommentId == effectivePriorityReplyId)
                    .Select(c => new ReplyCommentModel
                    {
                        CommentId = c.CommentId,
                        PostId = c.PostId,
                        ParentCommentId = c.ParentCommentId!.Value,
                        Owner = new AccountBasicInfoModel
                        {
                            AccountId = c.Account.AccountId,
                            FullName = c.Account.FullName,
                            Username = c.Account.Username,
                            AvatarUrl = c.Account.AvatarUrl,
                            Status = c.Account.Status
                        },
                        Content = c.Content,
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt,
                        ReactCount = _context.CommentReacts.Count(r => r.CommentId == c.CommentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                        IsCommentReactedByCurrentUser = currentId != null && _context.CommentReacts.Any(r => r.CommentId == c.CommentId && r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                        PostOwnerId = postOwnerId
                    })
                    .FirstOrDefaultAsync();
            }

            if (priorityItem != null)
            {
                query = query.Where(c => c.CommentId != priorityItem.CommentId);
            }
            else if (effectivePriorityReplyId != Guid.Empty)
            {
                query = query.Where(c => c.CommentId != effectivePriorityReplyId);
            }

            var rawItems = await query
                .OrderBy(c => c.CreatedAt) // Replies usually ordered ascending
                .ThenBy(c => c.CommentId)
                .Take(pageSize + 1)
                .Select(c => new ReplyCommentModel
                {
                    CommentId = c.CommentId,
                    PostId = c.PostId,
                    ParentCommentId = c.ParentCommentId!.Value,
                    Owner = new AccountBasicInfoModel
                    {
                        AccountId = c.Account.AccountId,
                        FullName = c.Account.FullName,
                        Username = c.Account.Username,
                        AvatarUrl = c.Account.AvatarUrl,
                        Status = c.Account.Status
                    },
                    Content = c.Content,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    ReactCount = _context.CommentReacts.Count(r => r.CommentId == c.CommentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    IsCommentReactedByCurrentUser = currentId != null && _context.CommentReacts.Any(r => r.CommentId == c.CommentId && r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    PostOwnerId = postOwnerId
                })
                .ToListAsync();

            var hasMore = rawItems.Count > pageSize;
            var pagedItems = hasMore
                ? rawItems.Take(pageSize).ToList()
                : rawItems;

            var items = priorityItem == null
                ? pagedItems
                : new[] { priorityItem }
                    .Concat(pagedItems)
                    .ToList();

            DateTime? nextCursorCreatedAt = null;
            Guid? nextCursorCommentId = null;
            if (hasMore && pagedItems.Count > 0)
            {
                var last = pagedItems[^1];
                nextCursorCreatedAt = last.CreatedAt;
                nextCursorCommentId = last.CommentId;
            }

            return (items, totalItems, nextCursorCreatedAt, nextCursorCommentId);
        }
    }
}

