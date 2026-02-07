using AutoMapper;
using SocialNetwork.Application.DTOs.AccountSettingDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Repositories.AccountSettingRepos;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.AccountSettingServices
{
    public class AccountSettingService : IAccountSettingService
    {
        private readonly IAccountSettingRepository _accountSettingRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        public AccountSettingService(IAccountSettingRepository accountSettingRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _accountSettingRepository = accountSettingRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
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

            await _unitOfWork.CommitAsync();
            return _mapper.Map<AccountSettingsResponse>(settings);
        }
    }
}

