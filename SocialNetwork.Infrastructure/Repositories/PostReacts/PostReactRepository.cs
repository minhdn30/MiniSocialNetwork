using SocialNetwork.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.PostReacts
{
    public class PostReactRepository : IPostReactRepository
    {
        private readonly AppDbContext _context;
        public PostReactRepository(AppDbContext context)
        {
            _context = context;
        }
    }
}
