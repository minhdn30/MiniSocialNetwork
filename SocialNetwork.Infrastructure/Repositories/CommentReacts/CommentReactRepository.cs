using Microsoft.EntityFrameworkCore;
using SocialNetwork.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.CommentReacts
{
    public class CommentReactRepository : ICommentReactRepository
    {
        private readonly AppDbContext _context;
        public CommentReactRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<int> CountCommentReactAsync (Guid commentId)
        {
            return await _context.CommentReacts.Where(cr => cr.CommentId == commentId).CountAsync();
        }
    }
}
