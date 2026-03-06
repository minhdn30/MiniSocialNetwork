using AutoMapper;
using CloudM.Application.DTOs.AccountSettingDTOs;
using CloudM.Application.Services.PresenceServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.AccountSettingRepos;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.AccountSettingServices
{
    public class AccountSettingService : IAccountSettingService
    {
        private readonly IAccountSettingRepository _accountSettingRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRealtimeService _realtimeService;
        private readonly IOnlinePresenceService _onlinePresenceService;

        public AccountSettingService(
            IAccountSettingRepository accountSettingRepository, 
            IMapper mapper, 
            IUnitOfWork unitOfWork,
            IRealtimeService realtimeService,
            IOnlinePresenceService onlinePresenceService)
        {
            _accountSettingRepository = accountSettingRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _realtimeService = realtimeService;
            _onlinePresenceService = onlinePresenceService;
        }

        public async Task<AccountSettingsResponse> GetSettingsByAccountIdAsync(Guid accountId)
        {
            var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(accountId);
            if (settings == null)
            {
                // Return default settings if not found in database per user request (don't create yet)
                settings = new AccountSettings { AccountId = accountId };
            }
            var response = _mapper.Map<AccountSettingsResponse>(settings);
            response.TagPermission = NormalizeTagPermission(response.TagPermission);
            return response;
        }

        public async Task<AccountSettingsResponse> UpdateSettingsAsync(Guid accountId, AccountSettingsUpdateRequest request)
        {
            var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(accountId);
            var previousOnlineStatusVisibility = settings?.OnlineStatusVisibility ?? OnlineStatusVisibilityEnum.ContactsOnly;
            if (settings == null)
            {
                // Create if not exists when updating
                settings = new AccountSettings { AccountId = accountId };
                _mapper.Map(request, settings);
                settings.TagPermission = NormalizeTagPermission(settings.TagPermission);
                await _accountSettingRepository.AddAccountSettingsAsync(settings);
            }
            else
            {
                _mapper.Map(request, settings);
                settings.TagPermission = NormalizeTagPermission(settings.TagPermission);
                await _accountSettingRepository.UpdateAccountSettingsAsync(settings);
            }

            await _unitOfWork.CommitAsync();
            var response = _mapper.Map<AccountSettingsResponse>(settings);
            response.TagPermission = NormalizeTagPermission(response.TagPermission);

            // Trigger real-time notification
            _ = _realtimeService.NotifyAccountSettingsUpdatedAsync(accountId, response);
            if (previousOnlineStatusVisibility != settings.OnlineStatusVisibility)
            {
                await _onlinePresenceService.NotifyVisibilityChangedAsync(
                    accountId,
                    previousOnlineStatusVisibility,
                    settings.OnlineStatusVisibility,
                    DateTime.UtcNow);
            }

            return response;
        }

        private static TagPermissionEnum NormalizeTagPermission(TagPermissionEnum value)
        {
            return value == TagPermissionEnum.NoOne
                ? TagPermissionEnum.NoOne
                : TagPermissionEnum.Anyone;
        }
    }
}

