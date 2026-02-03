using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostReactDTOs;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.CommentReactServices
{
    public interface ICommentReactService
    {
        Task<ReactToggleResponse> ToggleReactOnComment(Guid commentId, Guid accountId);
        Task<PagedResponse<AccountReactListModel>> GetAccountsReactOnCommentPaged(Guid commentId, Guid? currentId, int page, int pageSize);
    }
}
