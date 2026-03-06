using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.PostSaves
{
    public interface IPostSaveRepository
    {
        Task<bool> IsPostSavedByCurrentAsync(Guid currentId, Guid postId);
        Task<bool> TryAddPostSaveAsync(Guid currentId, Guid postId, DateTime createdAt);
        Task RemovePostSaveAsync(Guid currentId, Guid postId);
        Task<List<PostPersonalListModel>> GetSavedPostsByCurrentCursorAsync(
            Guid currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit);
    }
}
