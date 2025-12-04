using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.FollowDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Interfaces
{
    public interface IFollowService
    {
        Task<bool> FollowAsync(Guid followerId, Guid targetId);
        Task<bool> UnfollowAsync(Guid followerId, Guid targetId);
        Task<bool> IsFollowingAsync(Guid followerId, Guid targetId);
        Task<PagedResponse<AccountFollowListModel>> GetFollowersAsync(Guid userId, FollowPagingRequest request);
        Task<PagedResponse<AccountFollowListModel>> GetFollowingAsync(Guid userId, FollowPagingRequest request);
    }
}
