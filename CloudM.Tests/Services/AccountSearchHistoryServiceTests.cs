using FluentAssertions;
using Moq;
using CloudM.Application.Services.AccountSearchHistoryServices;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.AccountSearchHistories;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using Xunit;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class AccountSearchHistoryServiceTests
    {
        private readonly Mock<IAccountSearchHistoryRepository> _mockAccountSearchHistoryRepository;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly AccountSearchHistoryService _accountSearchHistoryService;

        public AccountSearchHistoryServiceTests()
        {
            _mockAccountSearchHistoryRepository = new Mock<IAccountSearchHistoryRepository>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _accountSearchHistoryService = new AccountSearchHistoryService(
                _mockAccountSearchHistoryRepository.Object,
                _mockUnitOfWork.Object);
        }

        [Fact]
        public async Task GetSidebarSearchHistoryAsync_WhenRepositoryReturnsItems_ReturnsMappedResults()
        {
            var currentId = Guid.NewGuid();
            var searchedAt = new DateTime(2026, 3, 12, 9, 0, 0, DateTimeKind.Utc);
            var repositoryResult = new List<SidebarAccountSearchModel>
            {
                new SidebarAccountSearchModel
                {
                    AccountId = Guid.NewGuid(),
                    Username = "minh",
                    FullName = "Minh Tran",
                    AvatarUrl = "avatar.jpg",
                    LastSearchedAt = searchedAt
                }
            };

            _mockAccountSearchHistoryRepository
                .Setup(x => x.GetSidebarSearchHistoryAsync(currentId, 12))
                .ReturnsAsync(repositoryResult);

            var result = await _accountSearchHistoryService.GetSidebarSearchHistoryAsync(currentId, 12);

            result.Should().ContainSingle();
            result[0].AccountId.Should().Be(repositoryResult[0].AccountId);
            result[0].Username.Should().Be("minh");
            result[0].FullName.Should().Be("Minh Tran");
            result[0].AvatarUrl.Should().Be("avatar.jpg");
            result[0].LastSearchedAt.Should().Be(searchedAt);
        }

        [Fact]
        public async Task SaveSidebarSearchHistoryAsync_WhenTargetUnavailable_ThrowsBadRequestException()
        {
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountSearchHistoryRepository
                .Setup(x => x.CanUseSidebarSearchTargetAsync(currentId, targetId))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<BadRequestException>(() =>
                _accountSearchHistoryService.SaveSidebarSearchHistoryAsync(currentId, targetId));

            _mockAccountSearchHistoryRepository.Verify(
                x => x.UpsertSidebarSearchHistoryAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>()),
                Times.Never);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task SaveSidebarSearchHistoryAsync_WhenTargetIsValid_UpsertsHistoryAndCommits()
        {
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountSearchHistoryRepository
                .Setup(x => x.CanUseSidebarSearchTargetAsync(currentId, targetId))
                .ReturnsAsync(true);
            _mockAccountSearchHistoryRepository
                .Setup(x => x.UpsertSidebarSearchHistoryAsync(currentId, targetId, It.IsAny<DateTime>()))
                .Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            await _accountSearchHistoryService.SaveSidebarSearchHistoryAsync(currentId, targetId);

            _mockAccountSearchHistoryRepository.Verify(
                x => x.UpsertSidebarSearchHistoryAsync(currentId, targetId, It.IsAny<DateTime>()),
                Times.Once);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task DeleteSidebarSearchHistoryAsync_WhenTargetIdIsEmpty_DoesNotDeleteOrCommit()
        {
            var currentId = Guid.NewGuid();

            await _accountSearchHistoryService.DeleteSidebarSearchHistoryAsync(currentId, Guid.Empty);

            _mockAccountSearchHistoryRepository.Verify(
                x => x.DeleteSidebarSearchHistoryAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
                Times.Never);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task DeleteSidebarSearchHistoryAsync_WhenTargetIdIsValid_DeletesAndCommits()
        {
            var currentId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            _mockAccountSearchHistoryRepository
                .Setup(x => x.DeleteSidebarSearchHistoryAsync(currentId, targetId))
                .Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            await _accountSearchHistoryService.DeleteSidebarSearchHistoryAsync(currentId, targetId);

            _mockAccountSearchHistoryRepository.Verify(
                x => x.DeleteSidebarSearchHistoryAsync(currentId, targetId),
                Times.Once);
            _mockUnitOfWork.Verify(x => x.CommitAsync(), Times.Once);
        }
    }
}
