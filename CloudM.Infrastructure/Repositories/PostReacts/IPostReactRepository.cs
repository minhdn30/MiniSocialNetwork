using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.PostReacts
{
    public interface IPostReactRepository
    {
        Task AddPostReact(PostReact postReact);
        Task RemovePostReact(PostReact postReact);
        Task<int> GetReactCountByPostId(Guid postId);
        Task<PostReact?> GetUserReactOnPostAsync(Guid postId, Guid accountId);
        Task<bool> IsCurrentUserReactedOnPostAsync(Guid postId, Guid? currentId);
        Task<(List<AccountReactListModel> reacts, int totalItems)> GetAccountsReactOnPostPaged(Guid postId, Guid? currentId, int page, int pageSize);
    }
}
