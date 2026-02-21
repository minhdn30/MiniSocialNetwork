using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.Services.AuthServices;
using SocialNetwork.Application.Services.JwtServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.AccountSettingRepos;
using SocialNetwork.Infrastructure.Repositories.ExternalLogins;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Tests.Helpers;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Tests.Services
{
    public class AuthServiceTests
    {
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IAccountSettingRepository> _accountSettingRepositoryMock;
        private readonly Mock<IExternalLoginRepository> _externalLoginRepositoryMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IJwtService> _jwtServiceMock;
        private readonly Mock<ILoginRateLimitService> _loginRateLimitServiceMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _accountSettingRepositoryMock = new Mock<IAccountSettingRepository>();
            _externalLoginRepositoryMock = new Mock<IExternalLoginRepository>();
            _mapperMock = new Mock<IMapper>();
            _jwtServiceMock = new Mock<IJwtService>();
            _loginRateLimitServiceMock = new Mock<ILoginRateLimitService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _loginRateLimitServiceMock
                .Setup(x => x.EnforceLoginAllowedAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>()))
                .Returns(Task.CompletedTask);
            _loginRateLimitServiceMock
                .Setup(x => x.RecordFailedAttemptAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>()))
                .Returns(Task.CompletedTask);
            _loginRateLimitServiceMock
                .Setup(x => x.ClearFailedAttemptsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>()))
                .Returns(Task.CompletedTask);
            _externalLoginRepositoryMock
                .Setup(x => x.CountByAccountIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(0);

            _authService = new AuthService(
                _accountRepositoryMock.Object,
                _externalLoginRepositoryMock.Object,
                _accountSettingRepositoryMock.Object,
                _mapperMock.Object,
                _jwtServiceMock.Object,
                _loginRateLimitServiceMock.Object,
                _unitOfWorkMock.Object,
                new List<IExternalIdentityProvider>()
            );
        }

        #region RegisterAsync Tests

        [Fact]
        public async Task RegisterAsync_ValidRequest_ReturnsRegisterResponse()
        {
            // Arrange
            var request = TestDataFactory.CreateRegisterDTO();
            var account = TestDataFactory.CreateAccount();
            var expectedResponse = new RegisterResponse { AccountId = account.AccountId };

            _accountRepositoryMock.Setup(x => x.IsUsernameExist(request.Username)).ReturnsAsync(false);
            _accountRepositoryMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(false);
            _mapperMock.Setup(x => x.Map<Account>(request)).Returns(account);
            _mapperMock.Setup(x => x.Map<RegisterResponse>(account)).Returns(expectedResponse);
            _unitOfWorkMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _authService.RegisterAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.AccountId.Should().Be(account.AccountId);
            account.Status.Should().Be(AccountStatusEnum.EmailNotVerified);
            account.Settings.Should().NotBeNull();
            _accountRepositoryMock.Verify(x => x.AddAccount(It.IsAny<Account>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_UsernameExists_ThrowsBadRequestException()
        {
            // Arrange
            var request = TestDataFactory.CreateRegisterDTO();
            _accountRepositoryMock.Setup(x => x.IsUsernameExist(request.Username)).ReturnsAsync(true);

            // Act
            var act = () => _authService.RegisterAsync(request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Username already exists.");
        }

        [Fact]
        public async Task RegisterAsync_EmailExists_ThrowsBadRequestException()
        {
            // Arrange
            var request = TestDataFactory.CreateRegisterDTO();
            _accountRepositoryMock.Setup(x => x.IsUsernameExist(request.Username)).ReturnsAsync(false);
            _accountRepositoryMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(true);

            // Act
            var act = () => _authService.RegisterAsync(request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Email already exists.");
        }

        #endregion

        #region LoginAsync Tests

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsLoginResponse()
        {
            // Arrange
            var request = TestDataFactory.CreateLoginRequest();
            var account = TestDataFactory.CreateAccount(email: request.Email);
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            account.Status = AccountStatusEnum.Active;

            var accessToken = "test-access-token";
            var settings = new AccountSettings { DefaultPostPrivacy = PostPrivacyEnum.Public };

            _accountRepositoryMock.Setup(x => x.GetAccountByEmail(request.Email)).ReturnsAsync(account);
            _jwtServiceMock.Setup(x => x.GenerateToken(account)).Returns(accessToken);
            _accountSettingRepositoryMock.Setup(x => x.GetGetAccountSettingsByAccountIdAsync(account.AccountId))
                .ReturnsAsync(settings);
            _unitOfWorkMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _authService.LoginAsync(request);

            // Assert
            result.Should().NotBeNull();
            result!.AccessToken.Should().Be(accessToken);
            result.AccountId.Should().Be(account.AccountId);
            _accountRepositoryMock.Verify(x => x.UpdateAccount(account), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_InvalidEmail_ThrowsUnauthorizedException()
        {
            // Arrange
            var request = TestDataFactory.CreateLoginRequest();
            _accountRepositoryMock.Setup(x => x.GetAccountByEmail(request.Email)).ReturnsAsync((Account?)null);

            // Act
            var act = () => _authService.LoginAsync(request);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage("Invalid email or password.");
        }

        [Fact]
        public async Task LoginAsync_InvalidPassword_ThrowsUnauthorizedException()
        {
            // Arrange
            var request = TestDataFactory.CreateLoginRequest();
            var account = TestDataFactory.CreateAccount(email: request.Email);
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("DifferentPassword");

            _accountRepositoryMock.Setup(x => x.GetAccountByEmail(request.Email)).ReturnsAsync(account);

            // Act
            var act = () => _authService.LoginAsync(request);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage("Invalid email or password.");
        }

        [Theory]
        [InlineData(AccountStatusEnum.Banned)]
        [InlineData(AccountStatusEnum.Suspended)]
        [InlineData(AccountStatusEnum.Deleted)]
        public async Task LoginAsync_RestrictedAccount_ThrowsUnauthorizedException(AccountStatusEnum status)
        {
            // Arrange
            var request = TestDataFactory.CreateLoginRequest();
            var account = TestDataFactory.CreateAccount(email: request.Email, status: status);
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            _accountRepositoryMock.Setup(x => x.GetAccountByEmail(request.Email)).ReturnsAsync(account);

            // Act
            var act = () => _authService.LoginAsync(request);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage("Your account has been restricted. Please contact support.");
        }

        [Fact]
        public async Task LoginAsync_EmailNotVerified_ThrowsUnauthorizedException()
        {
            // Arrange
            var request = TestDataFactory.CreateLoginRequest();
            var account = TestDataFactory.CreateAccount(email: request.Email);
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            account.Status = AccountStatusEnum.EmailNotVerified;

            _accountRepositoryMock.Setup(x => x.GetAccountByEmail(request.Email)).ReturnsAsync(account);

            // Act
            var act = () => _authService.LoginAsync(request);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage("Email is not verified. Please verify your email.");
        }

        #endregion

        #region RefreshTokenAsync Tests

        [Fact]
        public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
        {
            // Arrange
            var refreshToken = "valid-refresh-token";
            var account = TestDataFactory.CreateAccount();
            account.RefreshToken = refreshToken;
            account.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1);
            var newAccessToken = "new-access-token";

            _accountRepositoryMock.Setup(x => x.GetByRefreshToken(refreshToken)).ReturnsAsync(account);
            _jwtServiceMock.Setup(x => x.GenerateToken(account)).Returns(newAccessToken);
            _accountSettingRepositoryMock.Setup(x => x.GetGetAccountSettingsByAccountIdAsync(account.AccountId))
                .ReturnsAsync((AccountSettings?)null);
            _unitOfWorkMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _authService.RefreshTokenAsync(refreshToken);

            // Assert
            result.Should().NotBeNull();
            result!.AccessToken.Should().Be(newAccessToken);
            result.RefreshToken.Should().NotBe(refreshToken);
            _accountRepositoryMock.Verify(x => x.UpdateAccount(account), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task RefreshTokenAsync_EmptyToken_ThrowsUnauthorizedException()
        {
            // Arrange
            var refreshToken = "";

            // Act
            var act = () => _authService.RefreshTokenAsync(refreshToken);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage("Invalid or expired refresh token.");
        }

        [Fact]
        public async Task RefreshTokenAsync_InvalidToken_ThrowsUnauthorizedException()
        {
            // Arrange
            var refreshToken = "invalid-token";
            _accountRepositoryMock.Setup(x => x.GetByRefreshToken(refreshToken)).ReturnsAsync((Account?)null);

            // Act
            var act = () => _authService.RefreshTokenAsync(refreshToken);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage("Invalid or expired refresh token.");
        }

        [Fact]
        public async Task RefreshTokenAsync_ExpiredToken_ThrowsUnauthorizedException()
        {
            // Arrange
            var refreshToken = "expired-token";
            var account = TestDataFactory.CreateAccount();
            account.RefreshToken = refreshToken;
            account.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(-1); // Expired

            _accountRepositoryMock.Setup(x => x.GetByRefreshToken(refreshToken)).ReturnsAsync(account);

            // Act
            var act = () => _authService.RefreshTokenAsync(refreshToken);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage("Invalid or expired refresh token.");
        }

        #endregion

        #region LogoutAsync Tests

        [Fact]
        public async Task LogoutAsync_ValidAccount_ClearsRefreshToken()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var account = TestDataFactory.CreateAccount(accountId: accountId);
            account.RefreshToken = "some-token";
            var mockResponse = new Mock<HttpResponse>();
            var mockCookies = new Mock<IResponseCookies>();
            mockResponse.Setup(x => x.Cookies).Returns(mockCookies.Object);

            _accountRepositoryMock.Setup(x => x.GetAccountById(accountId)).ReturnsAsync(account);
            _unitOfWorkMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Act
            await _authService.LogoutAsync(accountId, mockResponse.Object);

            // Assert
            account.RefreshToken.Should().BeNull();
            account.RefreshTokenExpiryTime.Should().BeNull();
            _accountRepositoryMock.Verify(x => x.UpdateAccount(account), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
            mockCookies.Verify(x => x.Delete("refreshToken"), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_AccountNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var mockResponse = new Mock<HttpResponse>();
            _accountRepositoryMock.Setup(x => x.GetAccountById(accountId)).ReturnsAsync((Account?)null);

            // Act
            var act = () => _authService.LogoutAsync(accountId, mockResponse.Object);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("Account not found.");
        }

        #endregion
    }
}
