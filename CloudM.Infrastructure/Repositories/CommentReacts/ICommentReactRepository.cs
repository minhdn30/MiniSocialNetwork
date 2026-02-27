using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.CommentReacts
{
    public interface ICommentReactRepository
    {
        Task<int> CountCommentReactAsync(Guid commentId);
        Task AddCommentReact(CommentReact commentReact);
        Task RemoveCommentReact(CommentReact commentReact);
        Task<int> GetReactCountByCommentId(Guid commentId);
        Task<CommentReact?> GetUserReactOnCommentAsync(Guid commentId, Guid accountId);
        Task<bool> IsCurrentUserReactedOnCommentAsync(Guid commentId, Guid? currentId);
        Task<(List<AccountReactListModel> reacts, int totalItems)> GetAccountsReactOnCommentPaged(Guid commentId, Guid? currentId, int page, int pageSize);

    }
}
