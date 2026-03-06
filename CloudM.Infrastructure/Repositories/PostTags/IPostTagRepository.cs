using System;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.PostTags
{
    public interface IPostTagRepository
    {
        Task<bool> RemoveCurrentUserTagAsync(Guid postId, Guid currentId);
    }
}
