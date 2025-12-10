using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Follows
{
    public interface IFollowRepository
    {
        Task<bool> IsFollowingAsync(Guid followerId, Guid followedId);
        Task AddFollowAsync(Follow follow);
        Task RemoveFollowAsync(Guid followerId, Guid followedId);
        Task<List<Guid>> GetFollowingIdsAsync(Guid followerId);
        Task<List<Guid>> GetFollowerIdsAsync(Guid followedId);
        Task<(List<AccountFollowListModel> Items, int TotalItems)> GetFollowersAsync(Guid accountId, string? keyword, int page, int pageSize);
        Task<(List<AccountFollowListModel> Items, int TotalItems)> GetFollowingAsync(Guid accountId, string? keyword, int page, int pageSize);
        Task<int> CountFollowersAsync(Guid accountId);
        Task<int> CountFollowingAsync(Guid accountId);

    }
}
