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
        Task<PagedResponse<CommentResponse>> GetCommentsByPostIdAsync(Guid postId, Guid? currentId, int page, int pageSize);
        Task<PagedResponse<CommentResponse>> GetRepliesByCommentIdAsync(Guid commentId, Guid? currentId, int page, int pageSize);
        Task<CommentResponse?> GetCommentByIdAsync(Guid commentId);
        Task<int> GetReplyCountAsync(Guid commentId);
    }
}
