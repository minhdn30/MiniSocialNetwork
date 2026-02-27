using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.PostReactDTOs;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.CommentReactServices
{
    public interface ICommentReactService
    {
        Task<ReactToggleResponse> ToggleReactOnComment(Guid commentId, Guid accountId);
        Task<PagedResponse<AccountReactListModel>> GetAccountsReactOnCommentPaged(Guid commentId, Guid? currentId, int page, int pageSize);
    }
}
