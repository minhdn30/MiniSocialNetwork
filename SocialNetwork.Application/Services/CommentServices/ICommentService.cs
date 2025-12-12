using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.CommentServices
{
    public interface ICommentService
    {
        Task<CommentResponse> AddCommentAsync(Guid postId, Guid accountId, CommentCreateRequest request);
        Task<CommentResponse> UpdateCommentAsync(Guid commentId, Guid accountId, CommentUpdateRequest request);
        Task<Guid?> DeleteCommentAsync(Guid commentId, Guid accountId, bool isAdmin);
        Task<PagedResponse<CommentWithReplyCountModel>> GetCommentsByPostIdAsync(Guid postId, Guid? currentId, int page, int pageSize);
    }
}
