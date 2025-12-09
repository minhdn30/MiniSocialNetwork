using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;
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
    }
}
