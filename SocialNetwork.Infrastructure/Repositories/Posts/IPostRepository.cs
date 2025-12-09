using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
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
        Task AddPost(Post post);
    }
}
