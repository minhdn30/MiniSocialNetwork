using CloudM.Application.DTOs.AccountSettingDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.AccountSettingServices
{
    public interface IAccountSettingService
    {
        Task<AccountSettingsResponse> GetSettingsByAccountIdAsync(Guid accountId);
        Task<AccountSettingsResponse> UpdateSettingsAsync(Guid accountId, AccountSettingsUpdateRequest request);
    }
}
