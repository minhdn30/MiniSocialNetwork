using SocialNetwork.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Comments
{
    public interface ICommentRepository
    {
        Task AddComment(Comment comment);
        Task UpdateComment(Comment comment);
    }
}
