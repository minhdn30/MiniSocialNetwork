using CloudM.Application.DTOs.CommentDTOs;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.CommentServices
{
    public interface ICommentService
    {
        Task<CommentResponse> AddCommentAsync(Guid postId, Guid accountId, CommentCreateRequest request);
        Task<CommentResponse> UpdateCommentAsync(Guid commentId, Guid accountId, CommentUpdateRequest request);
        Task<CommentDeleteResult> DeleteCommentAsync(Guid commentId, Guid accountId, bool isAdmin);
        Task<CommentCursorResponse> GetCommentsByPostIdAsync(Guid postId, Guid? currentId, DateTime? cursorCreatedAt, Guid? cursorCommentId, int pageSize);
        Task<CommentCursorResponse> GetRepliesByCommentIdAsync(Guid commentId, Guid? currentId, DateTime? cursorCreatedAt, Guid? cursorCommentId, int pageSize);
        Task<CommentResponse?> GetCommentByIdAsync(Guid commentId);
        Task<int> GetReplyCountAsync(Guid commentId);
    }
}
