using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostReactDTOs;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.PostReactServices
{
    public interface IPostReactService
    {
        Task<ReactToggleResponse> ToggleReactOnPost(Guid postId, Guid accountId);
        Task<PagedResponse<AccountReactListModel>> GetAccountsReactOnPostPaged(Guid postId, Guid? currentId, int page, int pageSize);
    }
}
