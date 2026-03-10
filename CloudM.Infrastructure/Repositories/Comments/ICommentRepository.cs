using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.Comments
{
    public interface ICommentRepository
    {
        Task<(IEnumerable<CommentWithReplyCountModel> items, int totalItems, DateTime? nextCursorCreatedAt, Guid? nextCursorCommentId)> GetCommentsByPostIdWithReplyCountAsync(Guid postId, Guid? currentId, DateTime? cursorCreatedAt, Guid? cursorCommentId, int pageSize);
        Task<Comment?> GetCommentById(Guid commentId);
        Task AddComment(Comment comment);
        Task UpdateComment(Comment comment);
        Task<bool> IsCommentExist(Guid commentId);
        Task<int> CountCommentsByPostId(Guid postId);
        Task DeleteCommentWithReplies(Guid commentId);
        Task<List<Comment>> GetCommentThreadForDeleteAsync(Guid commentId);
        Task<bool> IsCommentCanReply(Guid commentId);
        Task<int> CountCommentRepliesAsync(Guid commentId);
        Task<(IEnumerable<ReplyCommentModel> items, int totalItems, DateTime? nextCursorCreatedAt, Guid? nextCursorCommentId)> GetRepliesByCommentIdAsync(Guid parentCommentId, Guid? currentId, DateTime? cursorCreatedAt, Guid? cursorCommentId, int pageSize);

    }
}
