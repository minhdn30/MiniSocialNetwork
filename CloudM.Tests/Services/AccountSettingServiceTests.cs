using AutoMapper;
using CloudM.Application.DTOs.AccountSettingDTOs;
using CloudM.Application.Mapping;
using CloudM.Application.Services.AccountSettingServices;
using CloudM.Application.Services.PresenceServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.AccountSettingRepos;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using FluentAssertions;
using Moq;

namespace CloudM.Tests.Services
{
    public class AccountSettingServiceTests
    {
        private readonly Mock<IAccountSettingRepository> _accountSettingRepository;
        private readonly Mock<IUnitOfWork> _unitOfWork;
        private readonly Mock<IRealtimeService> _realtimeService;
        private readonly Mock<IOnlinePresenceService> _onlinePresenceService;
        private readonly IMapper _mapper;
        private readonly AccountSettingService _service;

        public AccountSettingServiceTests()
        {
            _accountSettingRepository = new Mock<IAccountSettingRepository>();
            _unitOfWork = new Mock<IUnitOfWork>();
            _realtimeService = new Mock<IRealtimeService>();
            _onlinePresenceService = new Mock<IOnlinePresenceService>();

            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });
            _mapper = mapperConfig.CreateMapper();

            _service = new AccountSettingService(
                _accountSettingRepository.Object,
                _mapper,
                _unitOfWork.Object,
                _realtimeService.Object,
                _onlinePresenceService.Object);
        }

        [Fact]
        public async Task GetSettingsByAccountIdAsync_WhenSettingsNotFound_ShouldReturnDefaultLanguage()
        {
            var accountId = Guid.NewGuid();
            _accountSettingRepository
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(accountId))
                .ReturnsAsync((AccountSettings?)null);

            var result = await _service.GetSettingsByAccountIdAsync(accountId);

            result.AccountId.Should().Be(accountId);
            result.Language.Should().Be("en");
        }

        [Fact]
        public async Task GetSettingsByAccountIdAsync_WhenStoredLanguageIsInvalid_ShouldNormalizeToDefault()
        {
            var accountId = Guid.NewGuid();
            _accountSettingRepository
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(accountId))
                .ReturnsAsync(new AccountSettings
                {
                    AccountId = accountId,
                    Language = "fr"
                });

            var result = await _service.GetSettingsByAccountIdAsync(accountId);

            result.Language.Should().Be("en");
        }

        [Fact]
        public async Task UpdateSettingsAsync_WhenLanguageIsSupported_ShouldPersistNormalizedLanguage()
        {
            var accountId = Guid.NewGuid();
            var settings = new AccountSettings
            {
                AccountId = accountId,
                Language = "en",
                OnlineStatusVisibility = OnlineStatusVisibilityEnum.ContactsOnly
            };

            _accountSettingRepository
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(accountId))
                .ReturnsAsync(settings);

            var result = await _service.UpdateSettingsAsync(accountId, new AccountSettingsUpdateRequest
            {
                Language = "VI",
                DefaultPostPrivacy = PostPrivacyEnum.Private
            });

            settings.Language.Should().Be("vi");
            settings.DefaultPostPrivacy.Should().Be(PostPrivacyEnum.Private);
            result.Language.Should().Be("vi");
            _unitOfWork.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateSettingsAsync_WhenLanguageIsInvalid_ShouldFallbackToDefaultLanguage()
        {
            var accountId = Guid.NewGuid();
            var settings = new AccountSettings
            {
                AccountId = accountId,
                Language = "vi",
                FollowPrivacy = FollowPrivacyEnum.Private,
                OnlineStatusVisibility = OnlineStatusVisibilityEnum.ContactsOnly
            };

            _accountSettingRepository
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(accountId))
                .ReturnsAsync(settings);

            var result = await _service.UpdateSettingsAsync(accountId, new AccountSettingsUpdateRequest
            {
                Language = "fr",
                FollowPrivacy = FollowPrivacyEnum.Anyone
            });

            settings.Language.Should().Be("en");
            settings.FollowPrivacy.Should().Be(FollowPrivacyEnum.Anyone);
            result.Language.Should().Be("en");
        }

        [Fact]
        public async Task UpdateSettingsAsync_WhenLanguageIsExplicitlyNull_ShouldResetToDefaultLanguage()
        {
            var accountId = Guid.NewGuid();
            var settings = new AccountSettings
            {
                AccountId = accountId,
                Language = "vi",
                OnlineStatusVisibility = OnlineStatusVisibilityEnum.ContactsOnly
            };

            _accountSettingRepository
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(accountId))
                .ReturnsAsync(settings);

            var request = new AccountSettingsUpdateRequest
            {
                Language = null,
                DefaultPostPrivacy = PostPrivacyEnum.Public
            };

            var result = await _service.UpdateSettingsAsync(accountId, request);

            request.HasLanguage.Should().BeTrue();
            settings.Language.Should().Be("en");
            settings.DefaultPostPrivacy.Should().Be(PostPrivacyEnum.Public);
            result.Language.Should().Be("en");
        }

        [Fact]
        public async Task UpdateSettingsAsync_WhenLanguageIsNotProvided_ShouldPreserveExistingLanguage()
        {
            var accountId = Guid.NewGuid();
            var settings = new AccountSettings
            {
                AccountId = accountId,
                Language = "vi",
                FollowPrivacy = FollowPrivacyEnum.Private,
                OnlineStatusVisibility = OnlineStatusVisibilityEnum.ContactsOnly
            };

            _accountSettingRepository
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(accountId))
                .ReturnsAsync(settings);

            var request = new AccountSettingsUpdateRequest
            {
                FollowPrivacy = FollowPrivacyEnum.Anyone
            };

            var result = await _service.UpdateSettingsAsync(accountId, request);

            request.HasLanguage.Should().BeFalse();
            settings.Language.Should().Be("vi");
            settings.FollowPrivacy.Should().Be(FollowPrivacyEnum.Anyone);
            result.Language.Should().Be("vi");
        }

        [Fact]
        public async Task UpdateSettingsAsync_WhenOnlyLanguageIsProvided_ShouldPreserveExistingSettings()
        {
            var accountId = Guid.NewGuid();
            var settings = new AccountSettings
            {
                AccountId = accountId,
                Language = "en",
                PhonePrivacy = AccountPrivacyEnum.Private,
                AddressPrivacy = AccountPrivacyEnum.FollowOnly,
                DefaultPostPrivacy = PostPrivacyEnum.Private,
                FollowerPrivacy = AccountPrivacyEnum.Private,
                FollowingPrivacy = AccountPrivacyEnum.FollowOnly,
                FollowPrivacy = FollowPrivacyEnum.Private,
                StoryHighlightPrivacy = AccountPrivacyEnum.FollowOnly,
                GroupChatInvitePermission = GroupChatInvitePermissionEnum.FollowersOrFollowing,
                OnlineStatusVisibility = OnlineStatusVisibilityEnum.NoOne,
                TagPermission = TagPermissionEnum.NoOne
            };

            _accountSettingRepository
                .Setup(x => x.GetGetAccountSettingsByAccountIdAsync(accountId))
                .ReturnsAsync(settings);

            var result = await _service.UpdateSettingsAsync(accountId, new AccountSettingsUpdateRequest
            {
                Language = "vi"
            });

            settings.Language.Should().Be("vi");
            settings.PhonePrivacy.Should().Be(AccountPrivacyEnum.Private);
            settings.AddressPrivacy.Should().Be(AccountPrivacyEnum.FollowOnly);
            settings.DefaultPostPrivacy.Should().Be(PostPrivacyEnum.Private);
            settings.FollowerPrivacy.Should().Be(AccountPrivacyEnum.Private);
            settings.FollowingPrivacy.Should().Be(AccountPrivacyEnum.FollowOnly);
            settings.FollowPrivacy.Should().Be(FollowPrivacyEnum.Private);
            settings.StoryHighlightPrivacy.Should().Be(AccountPrivacyEnum.FollowOnly);
            settings.GroupChatInvitePermission.Should().Be(GroupChatInvitePermissionEnum.FollowersOrFollowing);
            settings.OnlineStatusVisibility.Should().Be(OnlineStatusVisibilityEnum.NoOne);
            settings.TagPermission.Should().Be(TagPermissionEnum.NoOne);
            result.Language.Should().Be("vi");
            result.FollowPrivacy.Should().Be(FollowPrivacyEnum.Private);
        }
    }
}
