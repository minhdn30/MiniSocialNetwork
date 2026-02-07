using AutoMapper;
using FluentAssertions;
using Moq;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using SocialNetwork.Application.Services.ConversationServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Tests.Helpers;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Tests.Services
{
    public class ConversationServiceTests
    {
        private readonly Mock<IConversationRepository> _conversationRepositoryMock;
        private readonly Mock<IConversationMemberRepository> _conversationMemberRepositoryMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly ConversationService _conversationService;

        public ConversationServiceTests()
        {
            _conversationRepositoryMock = new Mock<IConversationRepository>();
            _conversationMemberRepositoryMock = new Mock<IConversationMemberRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _mapperMock = new Mock<IMapper>();

            _conversationService = new ConversationService(
                _conversationRepositoryMock.Object,
                _conversationMemberRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _mapperMock.Object,
                _accountRepositoryMock.Object
            );
        }

        #region GetPrivateConversationAsync Tests

        [Fact]
        public async Task GetPrivateConversationAsync_ConversationExists_ReturnsConversationResponse()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var conversation = TestDataFactory.CreateConversation();
            var expectedResponse = new ConversationResponse { ConversationId = conversation.ConversationId };

            _conversationRepositoryMock.Setup(x => x.GetConversationByTwoAccountIdsAsync(currentId, otherId))
                .ReturnsAsync(conversation);
            _mapperMock.Setup(x => x.Map<ConversationResponse>(conversation))
                .Returns(expectedResponse);

            // Act
            var result = await _conversationService.GetPrivateConversationAsync(currentId, otherId);

            // Assert
            result.Should().NotBeNull();
            result!.ConversationId.Should().Be(conversation.ConversationId);
        }

        [Fact]
        public async Task GetPrivateConversationAsync_ConversationNotExists_ReturnsNull()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            _conversationRepositoryMock.Setup(x => x.GetConversationByTwoAccountIdsAsync(currentId, otherId))
                .ReturnsAsync((Conversation?)null);

            // Act
            var result = await _conversationService.GetPrivateConversationAsync(currentId, otherId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetPrivateConversationAsync_SameUser_ThrowsBadRequestException()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            var act = () => _conversationService.GetPrivateConversationAsync(userId, userId);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Sender and receiver cannot be the same.");
        }

        #endregion

        #region CreatePrivateConversationAsync Tests

        [Fact]
        public async Task CreatePrivateConversationAsync_ValidRequest_CreatesAndReturnsConversation()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var conversation = TestDataFactory.CreateConversation();
            var expectedResponse = new ConversationResponse { ConversationId = conversation.ConversationId };

            _accountRepositoryMock.Setup(x => x.IsAccountIdExist(currentId)).ReturnsAsync(true);
            _accountRepositoryMock.Setup(x => x.IsAccountIdExist(otherId)).ReturnsAsync(true);
            _conversationRepositoryMock.Setup(x => x.IsPrivateConversationExistBetweenTwoAccounts(currentId, otherId))
                .ReturnsAsync(false);
            _conversationRepositoryMock.Setup(x => x.CreatePrivateConversationAsync(currentId, otherId))
                .ReturnsAsync(conversation);
            _mapperMock.Setup(x => x.Map<ConversationResponse>(conversation))
                .Returns(expectedResponse);

            // Act
            var result = await _conversationService.CreatePrivateConversationAsync(currentId, otherId);

            // Assert
            result.Should().NotBeNull();
            result.ConversationId.Should().Be(conversation.ConversationId);
        }

        [Fact]
        public async Task CreatePrivateConversationAsync_SameUser_ThrowsBadRequestException()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            var act = () => _conversationService.CreatePrivateConversationAsync(userId, userId);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Sender and receiver cannot be the same.");
        }

        [Fact]
        public async Task CreatePrivateConversationAsync_AccountNotExists_ThrowsNotFoundException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            _accountRepositoryMock.Setup(x => x.IsAccountIdExist(currentId)).ReturnsAsync(true);
            _accountRepositoryMock.Setup(x => x.IsAccountIdExist(otherId)).ReturnsAsync(false);

            // Act
            var act = () => _conversationService.CreatePrivateConversationAsync(currentId, otherId);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("One or both accounts do not exist.");
        }

        [Fact]
        public async Task CreatePrivateConversationAsync_ConversationAlreadyExists_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            _accountRepositoryMock.Setup(x => x.IsAccountIdExist(currentId)).ReturnsAsync(true);
            _accountRepositoryMock.Setup(x => x.IsAccountIdExist(otherId)).ReturnsAsync(true);
            _conversationRepositoryMock.Setup(x => x.IsPrivateConversationExistBetweenTwoAccounts(currentId, otherId))
                .ReturnsAsync(true);

            // Act
            var act = () => _conversationService.CreatePrivateConversationAsync(currentId, otherId);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("A private conversation between these two accounts already exists.");
        }

        #endregion
    }
}
