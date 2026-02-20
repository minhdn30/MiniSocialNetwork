using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using SocialNetwork.Application.Services.EmailVerificationServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.EmailVerifications;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Services.Email;
using System.Security.Cryptography;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Tests.Services
{
    public class EmailVerificationServiceTests
    {
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IEmailVerificationRepository> _emailVerificationRepositoryMock;
        private readonly Mock<IEmailVerificationRateLimitService> _rateLimitServiceMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly EmailVerificationService _service;

        public EmailVerificationServiceTests()
        {
            _emailServiceMock = new Mock<IEmailService>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _emailVerificationRepositoryMock = new Mock<IEmailVerificationRepository>();
            _rateLimitServiceMock = new Mock<IEmailVerificationRateLimitService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            var options = Options.Create(new EmailVerificationSecurityOptions
            {
                OtpExpiresMinutes = 5,
                ResendCooldownSeconds = 60,
                MaxSendsPerWindow = 3,
                SendWindowMinutes = 15,
                MaxSendsPerDay = 10,
                MaxFailedAttempts = 5,
                LockMinutes = 15,
                OtpPepper = "TEST_OTP_PEPPER"
            });

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<bool>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<bool>> operation, Func<Task>? _) => operation());
            _unitOfWorkMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            _service = new EmailVerificationService(
                _emailServiceMock.Object,
                _emailVerificationRepositoryMock.Object,
                _rateLimitServiceMock.Object,
                _accountRepositoryMock.Object,
                _unitOfWorkMock.Object,
                options);
        }

        [Fact]
        public async Task SendVerificationEmailAsync_RedisRateLimitUnavailable_FallsBackToSqlRateLimitAndSendsEmail()
        {
            var email = "fallback@test.com";
            var account = new Account
            {
                AccountId = Guid.NewGuid(),
                Email = email,
                Status = AccountStatusEnum.EmailNotVerified
            };
            var verification = new EmailVerification
            {
                Id = 10,
                Email = email,
                LastSentAt = DateTime.UtcNow.AddMinutes(-10),
                SendWindowStartedAt = DateTime.UtcNow.AddMinutes(-20),
                SendCountInWindow = 0,
                DailyWindowStartedAt = DateTime.UtcNow.Date,
                DailySendCount = 0,
                ExpiredAt = DateTime.UtcNow.AddMinutes(5),
                CodeHash = string.Empty,
                CodeSalt = string.Empty
            };

            _accountRepositoryMock
                .Setup(x => x.GetAccountByEmail(email))
                .ReturnsAsync(account);
            _rateLimitServiceMock
                .Setup(x => x.EnforceSendRateLimitAsync(email, It.IsAny<string?>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new InternalServerException("OTP service is temporarily unavailable. Please try again shortly."));
            _emailVerificationRepositoryMock
                .Setup(x => x.EnsureExistsByEmailAsync(email, It.IsAny<DateTime>()))
                .Returns(Task.CompletedTask);
            _emailVerificationRepositoryMock
                .Setup(x => x.GetLatestByEmailAsync(email))
                .ReturnsAsync(verification);
            _emailServiceMock
                .Setup(x => x.SendEmailAsync(email, It.IsAny<string>(), It.IsAny<string>(), true))
                .Returns(Task.CompletedTask);

            await _service.SendVerificationEmailAsync(email, "127.0.0.1");

            verification.SendCountInWindow.Should().Be(1);
            verification.DailySendCount.Should().Be(1);
            _emailServiceMock.Verify(
                x => x.SendEmailAsync(email, It.IsAny<string>(), It.IsAny<string>(), true),
                Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task VerifyEmailAsync_LegacyPbkdf2Hash_StillVerifiesSuccessfully()
        {
            var email = "legacy@test.com";
            var code = "123456";
            var pepper = "TEST_OTP_PEPPER";
            var saltBytes = RandomNumberGenerator.GetBytes(16);
            var legacyHash = Rfc2898DeriveBytes.Pbkdf2(
                $"{code}:{pepper}",
                saltBytes,
                100000,
                HashAlgorithmName.SHA256,
                32);

            var verification = new EmailVerification
            {
                Id = 20,
                Email = email,
                CodeHash = Convert.ToBase64String(legacyHash),
                CodeSalt = Convert.ToBase64String(saltBytes),
                ExpiredAt = DateTime.UtcNow.AddMinutes(3),
                SendWindowStartedAt = DateTime.UtcNow,
                DailyWindowStartedAt = DateTime.UtcNow.Date
            };

            var account = new Account
            {
                AccountId = Guid.NewGuid(),
                Email = email,
                Status = AccountStatusEnum.EmailNotVerified
            };

            _emailVerificationRepositoryMock
                .Setup(x => x.GetLatestByEmailAsync(email))
                .ReturnsAsync(verification);
            _emailVerificationRepositoryMock
                .Setup(x => x.TryMarkAsConsumedAsync(verification.Id, It.IsAny<DateTime>()))
                .ReturnsAsync(true);
            _emailVerificationRepositoryMock
                .Setup(x => x.DeleteEmailVerificationAsync(email))
                .Returns(Task.CompletedTask);
            _accountRepositoryMock
                .Setup(x => x.GetAccountByEmail(email))
                .ReturnsAsync(account);
            _accountRepositoryMock
                .Setup(x => x.UpdateAccount(account))
                .Returns(Task.CompletedTask);

            var result = await _service.VerifyEmailAsync(email, code);

            result.Should().BeTrue();
            account.Status.Should().Be(AccountStatusEnum.Active);
            _accountRepositoryMock.Verify(x => x.UpdateAccount(account), Times.Once);
            _emailVerificationRepositoryMock.Verify(x => x.DeleteEmailVerificationAsync(email), Times.Once);
        }
    }
}
