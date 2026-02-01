using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.PostMedias
{
    public class PostMediaRepository : IPostMediaRepository
    {
        private readonly AppDbContext _context;
        public PostMediaRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<PostMedia>> GetPostMediasByPostId(Guid postId)
        {
            return await _context.PostMedias
                .Where(pm => pm.PostId == postId)
                .ToListAsync();
        }
        public async Task<PostMedia?> GetPostMediaById(Guid postMediaId)
        {
            return await _context.PostMedias
                .FirstOrDefaultAsync(pm => pm.MediaId == postMediaId);
        }
        public async Task<bool> IsPostMediaExist(Guid postMediaId)
        {
            return await _context.PostMedias.AnyAsync(pm => pm.MediaId == postMediaId);
        }
        public async Task AddPostMedias(IEnumerable<PostMedia> medias)
        {
            await _context.PostMedias.AddRangeAsync(medias);
        }

        public async Task DeletePostMediasById(Guid postMediaId)
        {
            var media = await _context.PostMedias.FindAsync(postMediaId);
            if (media != null)
            {
                _context.PostMedias.Remove(media);
                await _context.SaveChangesAsync();
            }
        }
        public async Task DeletePostMedias(IEnumerable<PostMedia> postMedias)
        { 
            _context.PostMedias.RemoveRange(postMedias);
            await _context.SaveChangesAsync();
        }

    }
}
