using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Comments
{
    public class CommentRepository : ICommentRepository
    {
        private readonly AppDbContext _context;
        public CommentRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<(IEnumerable<CommentWithReplyCountModel> items, int totalItems)> GetCommentsByPostIdWithReplyCountAsync(Guid postId, Guid? currentId, int page, int pageSize)
        {
            if (page <= 0) page = 1;

            var totalItems = await _context.Comments
                .Where(c => c.PostId == postId && c.ParentCommentId == null && (c.Account.Status == AccountStatusEnum.Active || c.AccountId == currentId))
                .CountAsync();

            var postOwnerId = await _context.Posts
                .Where(p => p.PostId == postId)
                .Select(p => p.AccountId)
                .FirstOrDefaultAsync();

            var items = await _context.Comments
                .Where(c => c.PostId == postId && c.ParentCommentId == null && (c.Account.Status == AccountStatusEnum.Active || c.AccountId == currentId))
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                    ReactCount = _context.CommentReacts.Count(r => r.CommentId == c.CommentId),
                    ReplyCount = _context.Comments.Count(r => r.ParentCommentId == c.CommentId),
                    IsCommentReactedByCurrentUser = currentId != null && _context.CommentReacts.Any(r => r.CommentId == c.CommentId && r.AccountId == currentId),
                    PostOwnerId = postOwnerId
                })
                .ToListAsync();

            return (items, totalItems);
        }

        public async Task<Comment?> GetCommentById(Guid commentId)
        {
            return await _context.Comments.Include(c => c.Account).FirstOrDefaultAsync(c => c.CommentId == commentId && c.Account.Status == AccountStatusEnum.Active);
        }
        public async Task<Comment?> AddComment(Comment comment)
        {
            await _context.Comments.AddAsync(comment);
            await _context.SaveChangesAsync();
            return await _context.Comments.Include(c => c.Account).FirstOrDefaultAsync(c => c.CommentId == comment.CommentId);
        }
        public async Task UpdateComment(Comment comment)
        {
            _context.Comments.Update(comment);
            await _context.SaveChangesAsync();
        }
        public async Task<bool> IsCommentExist(Guid commentId)
        {
            return await _context.Comments.AnyAsync(c => c.CommentId == commentId && c.Account.Status == AccountStatusEnum.Active);
        }
        public async Task<int> CountCommentsByPostId(Guid postId)
        {
            //comment (not reply)
            return await _context.Comments.CountAsync(c => c.PostId == postId && c.ParentCommentId == null && c.Account.Status == AccountStatusEnum.Active);
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
            await _context.SaveChangesAsync();
        }
        public async Task<bool> IsCommentCanReply(Guid commentId)
        {
            return await _context.Comments.AnyAsync(c => c.CommentId == commentId && c.ParentCommentId == null && c.Account.Status == AccountStatusEnum.Active);
        }
        public async Task<int> CountCommentRepliesAsync(Guid commentId)
        {
            int count = 0;
            count += await _context.Comments.Where(c => c.ParentCommentId == commentId && c.Account.Status == AccountStatusEnum.Active).CountAsync();
            return count;
        }
        public async Task<(IEnumerable<ReplyCommentModel> items, int totalItems)> GetRepliesByCommentIdAsync(Guid parentCommentId, Guid? currentId, int page, int pageSize)
        {
            if (page <= 0) page = 1;

            var query = _context.Comments
                .Where(c => c.ParentCommentId == parentCommentId && (c.Account.Status == AccountStatusEnum.Active || c.AccountId == currentId));

            var totalItems = await query.CountAsync();

            var postOwnerId = await _context.Comments
                .Where(c => c.CommentId == parentCommentId)
                .Select(c => c.Post.AccountId)
                .FirstOrDefaultAsync();

            var items = await query
                .OrderBy(c => c.CreatedAt) // Replies usually ordered ascending
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                    ReactCount = _context.CommentReacts.Count(r => r.CommentId == c.CommentId),
                    IsCommentReactedByCurrentUser = currentId != null && _context.CommentReacts.Any(r => r.CommentId == c.CommentId && r.AccountId == currentId),
                    PostOwnerId = postOwnerId
                })
                .ToListAsync();

            return (items, totalItems);
        }
    }
}

