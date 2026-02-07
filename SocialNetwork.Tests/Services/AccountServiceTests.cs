using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.FollowDTOs;
using SocialNetwork.Application.Services.AccountServices;
using SocialNetwork.Application.Services.CloudinaryServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.AccountSettingRepos;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.Posts;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using Xunit;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Tests.Services
{
    public class AccountServiceTests
    {
        private readonly Mock<IAccountRepository> _mockAccountRepo;
        private readonly Mock<IAccountSettingRepository> _mockAccountSettingRepo;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICloudinaryService> _mockCloudinaryService;
        private readonly Mock<IFollowRepository> _mockFollowRepo;
        private readonly Mock<IPostRepository> _mockPostRepo;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly AccountService _accountService;

        public AccountServiceTests()
        {
            _mockAccountRepo = new Mock<IAccountRepository>();
            _mockAccountSettingRepo = new Mock<IAccountSettingRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCloudinaryService = new Mock<ICloudinaryService>();
            _mockFollowRepo = new Mock<IFollowRepository>();
            _mockPostRepo = new Mock<IPostRepository>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();

            _accountService = new AccountService(
                _mockAccountRepo.Object,
                _mockAccountSettingRepo.Object,
                _mockMapper.Object,
                _mockCloudinaryService.Object,
                _mockFollowRepo.Object,
                _mockPostRepo.Object,
                _mockUnitOfWork.Object
            );
        }

        #region GetAccountByGuid Tests

        [Fact]
        public async Task GetAccountByGuid_WhenAccountExists_ReturnsAccountInfo()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var account = new Account { AccountId = accountId, Username = "testuser" };
            var mappedDetail = new AccountDetailResponse { AccountId = accountId };

            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync(account);
            _mockFollowRepo.Setup(x => x.CountFollowersAsync(accountId)).ReturnsAsync(10);
            _mockFollowRepo.Setup(x => x.CountFollowingAsync(accountId)).ReturnsAsync(5);
            _mockPostRepo.Setup(x => x.CountPostsByAccountIdAsync(accountId)).ReturnsAsync(25);
            _mockMapper.Setup(x => x.Map<AccountDetailResponse>(account)).Returns(mappedDetail);

            // Act
            var result = await _accountService.GetAccountByGuid(accountId);

            // Assert
            result.Value.Should().NotBeNull();
            result.Value!.AccountInfo.Should().NotBeNull();
            result.Value.FollowInfo!.Followers.Should().Be(10);
            result.Value.FollowInfo.Following.Should().Be(5);
            result.Value.TotalPosts.Should().Be(25);
        }

        [Fact]
        public async Task GetAccountByGuid_WhenAccountNotExists_ThrowsNotFoundException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync((Account?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _accountService.GetAccountByGuid(accountId));
        }

        #endregion

        #region CreateAccount Tests

        [Fact]
        public async Task CreateAccount_WhenUsernameExists_ThrowsBadRequestException()
        {
            // Arrange
            var request = new AccountCreateRequest { Username = "existinguser", Email = "test@example.com" };
            _mockAccountRepo.Setup(x => x.IsUsernameExist(request.Username)).ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _accountService.CreateAccount(request));
        }

        [Fact]
        public async Task CreateAccount_WhenEmailExists_ThrowsBadRequestException()
        {
            // Arrange
            var request = new AccountCreateRequest { Username = "newuser", Email = "existing@example.com" };
            _mockAccountRepo.Setup(x => x.IsUsernameExist(request.Username)).ReturnsAsync(false);
            _mockAccountRepo.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _accountService.CreateAccount(request));
        }

        [Fact]
        public async Task CreateAccount_WhenValid_CreatesAccountAndReturnsDetail()
        {
            // Arrange
            var request = new AccountCreateRequest { Username = "newuser", Email = "new@example.com", Password = "Password123!", RoleId = (int)RoleEnum.User };
            var account = new Account { AccountId = Guid.NewGuid(), Username = request.Username };
            var expectedResponse = new AccountDetailResponse { AccountId = account.AccountId };

            _mockAccountRepo.Setup(x => x.IsUsernameExist(request.Username)).ReturnsAsync(false);
            _mockAccountRepo.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(false);
            _mockMapper.Setup(x => x.Map<Account>(request)).Returns(account);
            _mockAccountRepo.Setup(x => x.AddAccount(It.IsAny<Account>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockMapper.Setup(x => x.Map<AccountDetailResponse>(account)).Returns(expectedResponse);

            // Act
            var result = await _accountService.CreateAccount(request);

            // Assert
            result.Should().NotBeNull();
            result.AccountId.Should().Be(account.AccountId);
        }

        #endregion

        #region UpdateAccount Tests

        [Fact]
        public async Task UpdateAccount_WhenAccountNotExists_ThrowsNotFoundException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync((Account?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _accountService.UpdateAccount(accountId, new AccountUpdateRequest()));
        }

        [Fact]
        public async Task UpdateAccount_WhenValid_UpdatesAndReturnsDetail()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var account = new Account { AccountId = accountId, Status = AccountStatusEnum.Active };
            var request = new AccountUpdateRequest { Status = AccountStatusEnum.Inactive };
            var expectedResponse = new AccountDetailResponse { AccountId = accountId };

            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync(account);
            _mockAccountRepo.Setup(x => x.UpdateAccount(It.IsAny<Account>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            _mockMapper.Setup(x => x.Map<AccountDetailResponse>(account)).Returns(expectedResponse);

            // Act
            var result = await _accountService.UpdateAccount(accountId, request);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region UpdateAccountProfile Tests

        [Fact]
        public async Task UpdateAccountProfile_WhenAccountNotExists_ThrowsNotFoundException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync((Account?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _accountService.UpdateAccountProfile(accountId, new ProfileUpdateRequest()));
        }

        #endregion

        #region GetAccountProfileByGuid Tests

        [Fact]
        public async Task GetAccountProfileByGuid_WhenProfileNotExists_ReturnsNull()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            _mockAccountRepo.Setup(x => x.GetProfileInfoAsync(accountId, null))
                .ReturnsAsync((ProfileInfoModel?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _accountService.GetAccountProfileByGuid(accountId, null));
        }

        [Fact]
        public async Task GetAccountProfileByGuid_WhenProfileExists_ReturnsProfile()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var profileModel = new ProfileInfoModel
            {
                AccountId = accountId,
                FollowerPrivacy = AccountPrivacyEnum.Public,
                FollowingPrivacy = AccountPrivacyEnum.Public
            };
            var expectedResponse = new ProfileInfoResponse
            {
                AccountInfo = new ProfileDetailResponse { AccountId = accountId },
                FollowInfo = new FollowCountResponse { Followers = 0, Following = 0 }
            };

            _mockAccountRepo.Setup(x => x.GetProfileInfoAsync(accountId, null)).ReturnsAsync(profileModel);
            _mockMapper.Setup(x => x.Map<ProfileInfoResponse>(profileModel)).Returns(expectedResponse);

            // Act
            var result = await _accountService.GetAccountProfileByGuid(accountId, null);

            // Assert
            result.Should().NotBeNull();
            result!.AccountInfo.AccountId.Should().Be(accountId);
        }

        #endregion

        #region GetAccountProfileByUsername Tests

        [Fact]
        public async Task GetAccountProfileByUsername_WhenProfileNotExists_ReturnsNull()
        {
            // Arrange
            var username = "nonexistent";
            _mockAccountRepo.Setup(x => x.GetProfileInfoByUsernameAsync(username, null))
                .ReturnsAsync((ProfileInfoModel?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _accountService.GetAccountProfileByUsername(username, null));
        }

        #endregion

        #region GetAccountProfilePreview Tests

        [Fact]
        public async Task GetAccountProfilePreview_WhenPreviewNotExists_ReturnsNull()
        {
            // Arrange
            var targetId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            _mockAccountRepo.Setup(x => x.GetProfilePreviewAsync(targetId, currentId))
                .ReturnsAsync((AccountProfilePreviewModel?)null);

            // Act
            var result = await _accountService.GetAccountProfilePreview(targetId, currentId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAccountProfilePreview_WhenPreviewExists_ReturnsPreview()
        {
            // Arrange
            var targetId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var preview = new AccountProfilePreviewModel
            {
                Account = new AccountBasicInfoModel { AccountId = targetId, FullName = "Test User" }
            };

            _mockAccountRepo.Setup(x => x.GetProfilePreviewAsync(targetId, currentId)).ReturnsAsync(preview);

            // Act
            var result = await _accountService.GetAccountProfilePreview(targetId, currentId);

            // Assert
            result.Should().NotBeNull();
            result!.Account.AccountId.Should().Be(targetId);
            result.Account.FullName.Should().Be("Test User");
        }

        #endregion

        #region ReactivateAccountAsync Tests

        [Fact]
        public async Task ReactivateAccountAsync_WhenAccountNotExists_ThrowsNotFoundException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync((Account?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _accountService.ReactivateAccountAsync(accountId));
        }

        [Fact]
        public async Task ReactivateAccountAsync_WhenAlreadyActive_ThrowsBadRequestException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var account = new Account { AccountId = accountId, Status = AccountStatusEnum.Active };

            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync(account);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _accountService.ReactivateAccountAsync(accountId));
        }

        [Fact]
        public async Task ReactivateAccountAsync_WhenInactive_ReactivatesSuccessfully()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var account = new Account { AccountId = accountId, Status = AccountStatusEnum.Inactive };

            _mockAccountRepo.Setup(x => x.GetAccountById(accountId)).ReturnsAsync(account);
            _mockAccountRepo.Setup(x => x.UpdateAccount(It.IsAny<Account>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            await _accountService.ReactivateAccountAsync(accountId);

            // Assert
            account.Status.Should().Be(AccountStatusEnum.Active);
        }

        #endregion
    }
}
