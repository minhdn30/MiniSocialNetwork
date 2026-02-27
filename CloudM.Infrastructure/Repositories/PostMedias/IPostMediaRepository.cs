using CloudM.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.PostMedias
{
    public interface IPostMediaRepository
    {
        Task<IEnumerable<PostMedia>> GetPostMediasByPostId(Guid postId);
        Task<PostMedia?> GetPostMediaById(Guid postMediaId);
        Task<bool> IsPostMediaExist(Guid postMediaId);
        Task AddPostMedias(IEnumerable<PostMedia> medias);
        Task DeletePostMediasById(Guid postMediaId);
        Task DeletePostMedias(IEnumerable<PostMedia> postMedias);
    }
}
