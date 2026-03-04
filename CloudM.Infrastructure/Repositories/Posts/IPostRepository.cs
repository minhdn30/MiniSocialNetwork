using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.Posts
{
    public interface IPostRepository
    {
        Task<Post?> GetPostById(Guid postId);
        Task<Post?> GetPostBasicInfoById(Guid postId);
        Task<Post?> GetPostForUpdateContent(Guid postId);
        //main detail
        Task<PostDetailModel?> GetPostDetailByPostId(Guid postId, Guid currentId);
        Task<PostDetailModel?> GetPostDetailByPostCode(string postCode, Guid currentId);
        Task AddPost(Post post);
        Task UpdatePost(Post post);
        Task SoftDeletePostAsync(Guid postId);
        Task<List<PostPersonalListModel>> GetPostsByAccountIdByCursor(
            Guid accountId,
            Guid? currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit);
        Task<int> CountPostsByAccountIdAsync(Guid accountId);
        Task<bool> IsPostExist(Guid postId);
        Task<bool> IsPostCodeExist(string postCode);
        Task<List<Guid>> GetTaggedAccountIdsByPostIdAsync(Guid postId);
        Task AddPostTagsAsync(IEnumerable<PostTag> postTags);
        Task RemovePostTagsAsync(Guid postId, IEnumerable<Guid> taggedAccountIds);
        Task<List<PostFeedModel>> GetFeedByTimelineAsync(Guid currentId, DateTime? cursorCreatedAt, Guid? cursorPostId, int limit);
        Task<List<PostFeedModel>> GetFeedByScoreAsync(Guid currentId, DateTime? cursorCreatedAt, Guid? cursorPostId, int limit);
    }
}
