using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Posts
{
    public class PostRepository : IPostRepository
    {
        private readonly AppDbContext _context;
        public PostRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<Post?> GetPostById(Guid postId)
        {
            return await _context.Posts
                .Include(p => p.Account)
                .Include(p => p.Medias)
                .Include(p => p.Reacts)
                .Include(p => p.Comments)
                .FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted);
        }
        public async Task AddPost(Post post)
        {
            await _context.Posts.AddAsync(post);
            await _context.SaveChangesAsync();
        }
        public async Task UpdatePost(Post post)
        {
            _context.Posts.Update(post);
            await _context.SaveChangesAsync();
        }
        public async Task SoftDeletePostAsync(Guid postId)
        {
            var post = await _context.Posts.FindAsync(postId);
            if (post != null)
            {
                post.IsDeleted = true;
                await _context.SaveChangesAsync();
            }
        }
        public async Task<(IEnumerable<PostPersonalListModel> posts, int TotalItems)> GetPostsByAccountId(Guid accountId, Guid? currentId, int page, int pageSize)
        {
            var query = _context.Posts
                .Where(p => p.AccountId == accountId && !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt);

            var totalItems = await query.CountAsync();

            var posts = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PostPersonalListModel
                {
                    PostId = p.PostId,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    Medias = p.Medias
                        .Select(m => new MediaPostPersonalListModel
                        {
                            MediaId = m.MediaId,
                            MediaUrl = m.MediaUrl,
                            Type = m.Type
                        })
                        .ToList(), 
                    MediaCount = p.Medias.Count(),
                    ReactCount = p.Reacts.Count(),
                    CommentCount = p.Comments.Count(),
                    IsReactedByCurrentUser = currentId != null && p.Reacts.Any(r => r.AccountId == currentId)
                })
                .ToListAsync();

            return (posts, totalItems);
        }
        public async Task<int> CountPostsByAccountIdAsync(Guid accountId)
        {
            return await _context.Posts
                .Where(p => p.AccountId == accountId && !p.IsDeleted)
                .CountAsync();
        }

    }
}
