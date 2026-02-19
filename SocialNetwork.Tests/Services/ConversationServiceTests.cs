using AutoMapper;
using FluentAssertions;
using Moq;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using SocialNetwork.Application.Services.ConversationServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.Messages;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using SocialNetwork.Tests.Helpers;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Tests.Services
{
    public class ConversationServiceTests
    {
        private readonly Mock<IConversationRepository> _conversationRepositoryMock;
        private readonly Mock<IConversationMemberRepository> _conversationMemberRepositoryMock;
        private readonly Mock<IMessageRepository> _messageRepositoryMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IFollowRepository> _followRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ICloudinaryService> _cloudinaryServiceMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly ConversationService _conversationService;

        public ConversationServiceTests()
        {
            _conversationRepositoryMock = new Mock<IConversationRepository>();
            _conversationMemberRepositoryMock = new Mock<IConversationMemberRepository>();
            _messageRepositoryMock = new Mock<IMessageRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _followRepositoryMock = new Mock<IFollowRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _cloudinaryServiceMock = new Mock<ICloudinaryService>();
            _mapperMock = new Mock<IMapper>();

            _conversationService = new ConversationService(
                _conversationRepositoryMock.Object,
                _conversationMemberRepositoryMock.Object,
                _messageRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _followRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _cloudinaryServiceMock.Object,
                _mapperMock.Object
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

        #region CreateGroupConversationAsync Tests

        [Fact]
        public async Task CreateGroupConversationAsync_ValidRequest_CreatesConversationMembersAndSystemMessage()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var member1Id = Guid.NewGuid();
            var member2Id = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var creator = new Account
            {
                AccountId = currentId,
                Username = "creator",
                FullName = "Group Creator",
                Status = AccountStatusEnum.Active
            };

            var member1 = new Account
            {
                AccountId = member1Id,
                Username = "member1",
                FullName = "Member One",
                Status = AccountStatusEnum.Active,
                Settings = new AccountSettings { GroupChatInvitePermission = GroupChatInvitePermissionEnum.Anyone }
            };

            var member2 = new Account
            {
                AccountId = member2Id,
                Username = "member2",
                FullName = "Member Two",
                Status = AccountStatusEnum.Active,
                Settings = new AccountSettings { GroupChatInvitePermission = GroupChatInvitePermissionEnum.Anyone }
            };

            Conversation? capturedConversation = null;
            List<ConversationMember>? capturedMembers = null;
            Message? capturedSystemMessage = null;

            _accountRepositoryMock.Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { creator, member1, member2 });
            _followRepositoryMock.Setup(x => x.GetConnectedAccountIdsAsync(currentId, It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new HashSet<Guid>());

            _conversationRepositoryMock.Setup(x => x.AddConversationAsync(It.IsAny<Conversation>()))
                .Callback<Conversation>(conv => capturedConversation = conv)
                .Returns(Task.CompletedTask);

            _conversationMemberRepositoryMock.Setup(x => x.AddConversationMembers(It.IsAny<List<ConversationMember>>()))
                .Callback<List<ConversationMember>>(members => capturedMembers = members)
                .Returns(Task.CompletedTask);

            _messageRepositoryMock.Setup(x => x.AddMessageAsync(It.IsAny<Message>()))
                .Callback<Message>(msg => capturedSystemMessage = msg)
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<bool>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<bool>> operation, Func<Task>? _) => operation());

            var avatarFileMock = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
            avatarFileMock.Setup(x => x.Length).Returns(128);
            _cloudinaryServiceMock
                .Setup(x => x.UploadImageAsync(avatarFileMock.Object))
                .ReturnsAsync("https://cdn.example.com/group-avatar.png");

            var request = new CreateGroupConversationRequest
            {
                GroupName = "Team Alpha",
                GroupAvatar = avatarFileMock.Object,
                MemberIds = new List<Guid> { member1Id, member2Id }
            };

            // Act
            var result = await _conversationService.CreateGroupConversationAsync(currentId, request);

            // Assert
            result.Should().NotBeNull();
            result.IsGroup.Should().BeTrue();
            result.ConversationName.Should().Be("Team Alpha");
            result.ConversationAvatar.Should().Be("https://cdn.example.com/group-avatar.png");
            result.CreatedBy.Should().Be(currentId);
            result.Members.Should().HaveCount(3);

            capturedConversation.Should().NotBeNull();
            capturedConversation!.IsGroup.Should().BeTrue();
            capturedConversation.ConversationName.Should().Be("Team Alpha");
            capturedConversation.ConversationAvatar.Should().Be("https://cdn.example.com/group-avatar.png");

            capturedMembers.Should().NotBeNull();
            capturedMembers!.Should().HaveCount(3);
            capturedMembers.Any(m => m.AccountId == currentId && m.IsAdmin).Should().BeTrue();

            capturedSystemMessage.Should().NotBeNull();
            capturedSystemMessage!.MessageType.Should().Be(MessageTypeEnum.System);
            capturedSystemMessage.Content.Should().Be("@creator created this group.");
            capturedSystemMessage.ConversationId.Should().Be(capturedConversation.ConversationId);
            capturedSystemMessage.SentAt.Should().BeOnOrAfter(now.AddMinutes(-1));
        }

        [Fact]
        public async Task CreateGroupConversationAsync_TargetPermissionNoOne_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var member1Id = Guid.NewGuid();
            var member2Id = Guid.NewGuid();

            var creator = new Account
            {
                AccountId = currentId,
                Username = "creator",
                FullName = "Group Creator",
                Status = AccountStatusEnum.Active
            };

            var blockedMember = new Account
            {
                AccountId = member1Id,
                Username = "blocked-user",
                FullName = "Blocked User",
                Status = AccountStatusEnum.Active,
                Settings = new AccountSettings { GroupChatInvitePermission = GroupChatInvitePermissionEnum.NoOne }
            };

            var allowedMember = new Account
            {
                AccountId = member2Id,
                Username = "allowed-user",
                FullName = "Allowed User",
                Status = AccountStatusEnum.Active,
                Settings = new AccountSettings { GroupChatInvitePermission = GroupChatInvitePermissionEnum.Anyone }
            };

            _accountRepositoryMock.Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { creator, blockedMember, allowedMember });
            _followRepositoryMock.Setup(x => x.GetConnectedAccountIdsAsync(currentId, It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new HashSet<Guid>());

            var request = new CreateGroupConversationRequest
            {
                GroupName = "Restricted Group",
                MemberIds = new List<Guid> { member1Id, member2Id }
            };

            // Act
            var act = () => _conversationService.CreateGroupConversationAsync(currentId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("*invite privacy*");
        }

        #endregion
    }
}
