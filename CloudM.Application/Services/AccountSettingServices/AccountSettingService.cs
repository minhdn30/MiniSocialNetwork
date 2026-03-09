using AutoMapper;
using CloudM.Application.DTOs.AccountSettingDTOs;
using CloudM.Application.Helpers;
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
            response.Language = LanguagePreferenceHelper.Normalize(settings.Language);
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
                ApplyPartialUpdate(settings, request);
                await _accountSettingRepository.AddAccountSettingsAsync(settings);
            }
            else
            {
                ApplyPartialUpdate(settings, request);
                await _accountSettingRepository.UpdateAccountSettingsAsync(settings);
            }

            await _unitOfWork.CommitAsync();
            var response = _mapper.Map<AccountSettingsResponse>(settings);
            response.TagPermission = NormalizeTagPermission(response.TagPermission);
            response.Language = LanguagePreferenceHelper.Normalize(settings.Language);

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

        private static void ApplyPartialUpdate(AccountSettings settings, AccountSettingsUpdateRequest request)
        {
            if (request.PhonePrivacy.HasValue)
            {
                settings.PhonePrivacy = request.PhonePrivacy.Value;
            }

            if (request.AddressPrivacy.HasValue)
            {
                settings.AddressPrivacy = request.AddressPrivacy.Value;
            }

            if (request.DefaultPostPrivacy.HasValue)
            {
                settings.DefaultPostPrivacy = request.DefaultPostPrivacy.Value;
            }

            if (request.FollowerPrivacy.HasValue)
            {
                settings.FollowerPrivacy = request.FollowerPrivacy.Value;
            }

            if (request.FollowingPrivacy.HasValue)
            {
                settings.FollowingPrivacy = request.FollowingPrivacy.Value;
            }

            if (request.FollowPrivacy.HasValue)
            {
                settings.FollowPrivacy = request.FollowPrivacy.Value;
            }

            if (request.StoryHighlightPrivacy.HasValue)
            {
                settings.StoryHighlightPrivacy = request.StoryHighlightPrivacy.Value;
            }

            if (request.GroupChatInvitePermission.HasValue)
            {
                settings.GroupChatInvitePermission = request.GroupChatInvitePermission.Value;
            }

            if (request.OnlineStatusVisibility.HasValue)
            {
                settings.OnlineStatusVisibility = request.OnlineStatusVisibility.Value;
            }

            if (request.TagPermission.HasValue)
            {
                settings.TagPermission = NormalizeTagPermission(request.TagPermission.Value);
            }
            else
            {
                settings.TagPermission = NormalizeTagPermission(settings.TagPermission);
            }

            if (request.HasLanguage)
            {
                settings.Language = LanguagePreferenceHelper.Normalize(request.Language);
            }
        }
    }
}

