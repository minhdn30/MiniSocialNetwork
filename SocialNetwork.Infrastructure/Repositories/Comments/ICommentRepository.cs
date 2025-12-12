using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Comments
{
    public interface ICommentRepository
    {
        Task<(IEnumerable<CommentWithReplyCountModel> items, int totalItems)> GetCommentsByPostIdWithReplyCountAsync(Guid postId, Guid? currentId, int page, int pageSize);
        Task<Comment?> GetCommentById(Guid commentId);
        Task<Comment?> AddComment(Comment comment);
        Task UpdateComment(Comment comment);
        Task<bool> IsCommentExist(Guid commentId);
        Task<int> CountCommentsByPostId(Guid postId);
        Task DeleteCommentWithReplies(Guid commentId);
        Task<bool> IsCommentCanReply(Guid commentId);
        Task<int> CountCommentRepliesAsync(Guid commentId);
    }
}
