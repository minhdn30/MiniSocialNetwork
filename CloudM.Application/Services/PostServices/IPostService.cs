using Microsoft.AspNetCore.Mvc;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.PostDTOs;
using CloudM.Application.DTOs.PostMediaDTOs;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.PostServices
{
    public interface IPostService
    {
        Task<PostDetailResponse?> GetPostById(Guid postId, Guid? currentId);
        Task<List<PostTaggedAccountResponse>> GetTaggedAccountsByPostId(Guid postId, Guid currentId);
        Task<PostDetailModel> GetPostDetailByPostId(Guid postId, Guid currentId);
        Task<PostDetailModel> GetPostDetailByPostCode(string postCode, Guid currentId);
        Task<PostDetailResponse> CreatePost(Guid accountId, PostCreateRequest request);
        Task<PostDetailResponse> UpdatePost(Guid postId, Guid currentId, PostUpdateRequest request);
        Task<PostUpdateContentResponse> UpdatePostContent(Guid postId, Guid currentId, PostUpdateContentRequest request);
        Task<Guid?> SoftDeletePost(Guid postId, Guid currentId, bool isAdmin);
        Task<(List<PostPersonalListModel> Items, bool HasMore)> GetPostsByAccountIdByCursorAsync(
            Guid accountId,
            Guid? currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit);
        Task<(List<PostPersonalListModel> Items, bool HasMore)> GetTaggedPostsByAccountIdByCursorAsync(
            Guid accountId,
            Guid currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit);
        Task<PostFeedCursorResponse> GetFeedPageAsync(Guid currentId, string? cursorToken, int limit);
        Task<List<PostFeedModel>> GetFeedByScoreAsync(Guid currentId, DateTime? cursorCreatedAt, Guid? cursorPostId, int limit);

    }
}
