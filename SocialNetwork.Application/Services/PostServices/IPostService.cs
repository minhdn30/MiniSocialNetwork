using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.DTOs.PostMediaDTOs;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.PostServices
{
    public interface IPostService
    {
        Task<PostDetailResponse?> GetPostById(Guid postId, Guid? currentId);
        Task<PostDetailResponse> CreatePost(Guid accountId, PostCreateRequest request);
        Task<PostDetailResponse> UpdatePost(Guid postId, Guid currentId, PostUpdateRequest request);
        Task<Guid?> SoftDeletePost(Guid postId, Guid currentId, bool isAdmin);
        Task<PagedResponse<PostPersonalListModel>> GetPostsByAccountId(Guid accountId, Guid? currentId, int page, int pageSize);

    }
}
