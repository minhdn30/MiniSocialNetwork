using SocialNetwork.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.AccountSettingRepos
{
    public interface IAccountSettingRepository
    {
        Task<AccountSettings?> GetGetAccountSettingsByAccountIdAsync(Guid accountId);
        Task AddAccountSettingsAsync(AccountSettings settings);
        Task UpdateAccountSettingsAsync(AccountSettings settings);
    }
}
