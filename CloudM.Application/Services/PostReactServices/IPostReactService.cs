using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.PostReactDTOs;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.PostReactServices
{
    public interface IPostReactService
    {
        Task<ReactToggleResponse> ToggleReactOnPost(Guid postId, Guid accountId);
        Task<PagedResponse<AccountReactListModel>> GetAccountsReactOnPostPaged(Guid postId, Guid? currentId, int page, int pageSize);
    }
}
