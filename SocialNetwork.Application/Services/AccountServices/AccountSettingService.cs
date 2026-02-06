using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Repositories.AccountSettingRepos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.AccountServices
{
    public class AccountSettingService : IAccountSettingService
    {
        private readonly IAccountSettingRepository _accountSettingRepository;
        private readonly IMapper _mapper;

        public AccountSettingService(IAccountSettingRepository accountSettingRepository, IMapper mapper)
        {
            _accountSettingRepository = accountSettingRepository;
            _mapper = mapper;
        }

        public async Task<AccountSettingsResponse> GetSettingsByAccountIdAsync(Guid accountId)
        {
            var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(accountId);
            if (settings == null)
            {
                // Return default settings if not found in database per user request (don't create yet)
                settings = new AccountSettings { AccountId = accountId };
            }
            return _mapper.Map<AccountSettingsResponse>(settings);
        }

        public async Task<AccountSettingsResponse> UpdateSettingsAsync(Guid accountId, AccountSettingsUpdateRequest request)
        {
            var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(accountId);
            if (settings == null)
            {
                // Create if not exists when updating
                settings = new AccountSettings { AccountId = accountId };
                _mapper.Map(request, settings);
                await _accountSettingRepository.AddAccountSettingsAsync(settings);
            }
            else
            {
                _mapper.Map(request, settings);
                await _accountSettingRepository.UpdateAccountSettingsAsync(settings);
            }
            return _mapper.Map<AccountSettingsResponse>(settings);
        }
    }
}
