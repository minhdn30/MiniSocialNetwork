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
        public async Task AddPostMedias(IEnumerable<PostMedia> medias)
        {
            await _context.PostMedias.AddRangeAsync(medias);
            await _context.SaveChangesAsync();
        }
    }
}
