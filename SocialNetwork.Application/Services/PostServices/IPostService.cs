using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.DTOs.PostMediaDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.PostServices
{
    public interface IPostService
    {
        Task<PostDetailResponse?> GetPostById(Guid postId);
        Task<PostDetailResponse> CreatePost([FromBody] PostCreateRequest request);
        Task<PostDetailResponse> UpdatePost(Guid postId, [FromBody] PostUpdateRequest request);
        Task SoftDeletePost(Guid postId);

    }
}
