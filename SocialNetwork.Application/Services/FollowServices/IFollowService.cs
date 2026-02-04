using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.FollowDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.FollowServices
{
    public interface IFollowService
    {
        Task<int> FollowAsync(Guid followerId, Guid targetId);
        Task<int> UnfollowAsync(Guid followerId, Guid targetId);
        Task<bool> IsFollowingAsync(Guid followerId, Guid targetId);
        Task<PagedResponse<AccountWithFollowStatusModel>> GetFollowersAsync(Guid userId, Guid? currentId, FollowPagingRequest request);
        Task<PagedResponse<AccountWithFollowStatusModel>> GetFollowingAsync(Guid userId, Guid? currentId, FollowPagingRequest request);
    }
}
