using SocialNetwork.Application.DTOs.AccountDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.AccountServices
{
    public interface IAccountSettingService
    {
        Task<AccountSettingsResponse> GetSettingsByAccountIdAsync(Guid accountId);
        Task<AccountSettingsResponse> UpdateSettingsAsync(Guid accountId, AccountSettingsUpdateRequest request);
    }
}
