using CloudM.Infrastructure.Models;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.FollowDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.FollowServices
{
    public interface IFollowService
    {
        Task<FollowCountResponse> FollowAsync(Guid followerId, Guid targetId);
        Task<FollowCountResponse> UnfollowAsync(Guid followerId, Guid targetId);
        Task<bool> IsFollowingAsync(Guid followerId, Guid targetId);
        Task<FollowCountResponse> GetRelationStatusAsync(Guid currentId, Guid targetId);
        Task AcceptFollowRequestAsync(Guid targetId, Guid requesterId);
        Task RemoveFollowRequestAsync(Guid targetId, Guid requesterId);
        Task RemoveFollowerAsync(Guid currentId, Guid followerId);
        Task<FollowRequestCursorResponse> GetPendingRequestsAsync(Guid currentId, FollowRequestCursorRequest request, CancellationToken cancellationToken = default);
        Task<PagedResponse<AccountWithFollowStatusModel>> GetSentPendingRequestsAsync(Guid currentId, FollowPagingRequest request);
        Task<PagedResponse<AccountWithFollowStatusModel>> GetFollowersAsync(Guid userId, Guid? currentId, FollowPagingRequest request);
        Task<PagedResponse<AccountWithFollowStatusModel>> GetFollowingAsync(Guid userId, Guid? currentId, FollowPagingRequest request);
        Task<FollowCountResponse> GetStatsAsync(Guid userId);
    }
}
