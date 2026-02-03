using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Posts
{
    public interface IPostRepository
    {
        Task<Post?> GetPostById(Guid postId);
        //main detail
        Task<PostDetailModel?> GetPostDetailByPostId(Guid postId, Guid currentId);
        Task AddPost(Post post);
        Task UpdatePost(Post post);
        Task SoftDeletePostAsync(Guid postId);
        Task<(IEnumerable<PostPersonalListModel> posts, int TotalItems)> GetPostsByAccountId(Guid accountId, Guid? currentId, int page, int pageSize);
        Task<int> CountPostsByAccountIdAsync(Guid accountId);
        Task<bool> IsPostExist(Guid postId);
        Task<List<PostFeedModel>> GetFeedByTimelineAsync(Guid currentId, DateTime? cursorCreatedAt, Guid? cursorPostId, int limit);
        Task<List<PostFeedModel>> GetFeedByScoreAsync(Guid currentId, DateTime? cursorCreatedAt, Guid? cursorPostId, int limit);
    }
}
