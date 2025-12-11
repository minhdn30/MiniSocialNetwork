using SocialNetwork.Application.DTOs.CommentDTOs;
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
    }
}
