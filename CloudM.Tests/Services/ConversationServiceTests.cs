using AutoMapper;
using FluentAssertions;
using Moq;
using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.ConversationDTOs;
using CloudM.Application.DTOs.MessageDTOs;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Application.Services.ConversationServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Infrastructure.Repositories.ConversationMembers;
using CloudM.Infrastructure.Repositories.Conversations;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.Messages;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Infrastructure.Services.Cloudinary;
using CloudM.Infrastructure.Models;
using CloudM.Tests.Helpers;
using System.Text.Json;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
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
        private readonly Mock<IRealtimeService> _realtimeServiceMock;
        private readonly Mock<IAccountBlockRepository> _accountBlockRepositoryMock;
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
            _realtimeServiceMock = new Mock<IRealtimeService>();
            _accountBlockRepositoryMock = new Mock<IAccountBlockRepository>();

            _messageRepositoryMock
                .Setup(x => x.AddMessageAsync(It.IsAny<Message>()))
                .Returns(Task.CompletedTask);

            _mapperMock
                .Setup(x => x.Map<SendMessageResponse>(It.IsAny<Message>()))
                .Returns(new SendMessageResponse());
            _mapperMock
                .Setup(x => x.Map<AccountChatInfoResponse>(It.IsAny<Account>()))
                .Returns((Account account) => new AccountChatInfoResponse
                {
                    AccountId = account.AccountId,
                    Username = account.Username,
                    FullName = account.FullName,
                    AvatarUrl = account.AvatarUrl,
                    IsActive = account.Status == AccountStatusEnum.Active
                });

            _realtimeServiceMock
                .Setup(x => x.NotifyNewMessageAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Dictionary<Guid, bool>>(),
                    It.IsAny<SendMessageResponse>()))
                .Returns(Task.CompletedTask);
            _realtimeServiceMock
                .Setup(x => x.NotifyGroupConversationInfoUpdatedAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid>()))
                .Returns(Task.CompletedTask);
            _accountBlockRepositoryMock
                .Setup(x => x.IsBlockedEitherWayAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(false);
            _accountBlockRepositoryMock
                .Setup(x => x.HasAnyRelationWithinAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<IEnumerable<Guid>?>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(false);
            _accountBlockRepositoryMock
                .Setup(x => x.GetRelationsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<AccountBlockRelationModel>());

            _conversationService = new ConversationService(
                _conversationRepositoryMock.Object,
                _conversationMemberRepositoryMock.Object,
                _messageRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _followRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _cloudinaryServiceMock.Object,
                _mapperMock.Object,
                _realtimeServiceMock.Object,
                null,
                null,
                _accountBlockRepositoryMock.Object
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
        public async Task GetPrivateConversationAsync_SameUser_ReturnsNull_WhenControllerValidationIsBypassed()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            var result = await _conversationService.GetPrivateConversationAsync(userId, userId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetConversationsByCursorAsync Tests

        [Fact]
        public async Task GetConversationsByCursorAsync_LastMessageHasReplyFlag_ShouldKeepReplyPreviewAfterReload()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var senderId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var items = new List<ConversationListModel>
            {
                new ConversationListModel
                {
                    ConversationId = conversationId,
                    IsGroup = false,
                    LastMessageSentAt = now,
                    LastMessage = new MessageBasicModel
                    {
                        MessageId = Guid.NewGuid(),
                        MessageType = MessageTypeEnum.Text,
                        Content = "alooo",
                        HasReply = true,
                        SentAt = now,
                        IsRecalled = false,
                        Sender = new AccountChatInfoModel
                        {
                            AccountId = senderId,
                            Username = "sender",
                            FullName = "Sender",
                            IsActive = true
                        }
                    }
                }
            };

            _conversationRepositoryMock
                .Setup(x => x.GetConversationsByCursorAsync(currentId, null, null, null, null, 20))
                .ReturnsAsync((items, false));

            // Act
            var result = await _conversationService.GetConversationsByCursorAsync(currentId, null, null, null, null, 20);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items.First().LastMessagePreview.Should().Be("Replied: alooo");
            result.HasMore.Should().BeFalse();
        }

        [Fact]
        public async Task GetConversationsByCursorAsync_WhenBlockedGroupMemberAvatarExists_ShouldKeepAvatarSlot()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var blockedMemberId = Guid.NewGuid();
            var allowedMemberId = Guid.NewGuid();

            var items = new List<ConversationListModel>
            {
                new ConversationListModel
                {
                    ConversationId = conversationId,
                    IsGroup = true,
                    DisplayName = "Project team",
                    GroupAvatars = new List<string>
                    {
                        "https://cdn.example.com/blocked.png",
                        "https://cdn.example.com/allowed.png"
                    },
                    GroupAvatarAccountIds = new List<Guid>
                    {
                        blockedMemberId,
                        allowedMemberId
                    }
                }
            };

            _conversationRepositoryMock
                .Setup(x => x.GetConversationsByCursorAsync(currentId, null, null, null, null, 20))
                .ReturnsAsync((items, false));
            // Act
            var result = await _conversationService.GetConversationsByCursorAsync(currentId, null, null, null, null, 20);

            // Assert
            result.Items.Should().HaveCount(1);
            result.Items[0].GroupAvatars.Should().Equal(
                "https://cdn.example.com/blocked.png",
                "https://cdn.example.com/allowed.png");
        }

        [Fact]
        public async Task GetConversationMessagesWithMetaDataAsync_WhenBlockedGroupMemberAvatarExists_ShouldKeepMetaAvatarSlot()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var blockedMemberId = Guid.NewGuid();
            var allowedMemberId = Guid.NewGuid();

            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, currentId))
                .ReturnsAsync(true);
            _messageRepositoryMock
                .Setup(x => x.GetMessagesByConversationId(conversationId, currentId, null, 20))
                .ReturnsAsync((Array.Empty<MessageBasicModel>(), null, null, false, false));
            _conversationRepositoryMock
                .Setup(x => x.GetConversationMetaDataAsync(conversationId, currentId))
                .ReturnsAsync(new ConversationListModel
                {
                    ConversationId = conversationId,
                    IsGroup = true,
                    DisplayName = "Project team",
                    GroupAvatars = new List<string>
                    {
                        "https://cdn.example.com/blocked.png",
                        "https://cdn.example.com/allowed.png"
                    },
                    GroupAvatarAccountIds = new List<Guid>
                    {
                        blockedMemberId,
                        allowedMemberId
                    }
                });
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMembersAsync(conversationId))
                .ReturnsAsync(new List<ConversationMember>());
            // Act
            var result = await _conversationService.GetConversationMessagesWithMetaDataAsync(conversationId, currentId, null, 20);

            // Assert
            result.MetaData.Should().NotBeNull();
            result.MetaData!.GroupAvatars.Should().Equal(
                "https://cdn.example.com/blocked.png",
                "https://cdn.example.com/allowed.png");
        }

        [Fact]
        public async Task SearchMessagesAsync_WhenSystemMessageContainsBlockedTargets_ShouldKeepPayloadLabels()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var blockedMemberId = Guid.NewGuid();

            var systemMessage = new MessageBasicModel
            {
                MessageId = Guid.NewGuid(),
                MessageType = MessageTypeEnum.System,
                Content = "@current added @blocked-user to the group.",
                SentAt = DateTime.UtcNow,
                Sender = new AccountChatInfoModel
                {
                    AccountId = currentId,
                    Username = "current",
                    FullName = "Current user",
                    IsActive = true
                },
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = 1,
                    actorAccountId = currentId,
                    actorUsername = "current",
                    targetAccountIds = new[] { blockedMemberId },
                    targetUsernames = new[] { "blocked-user" }
                })
            };

            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, currentId))
                .ReturnsAsync(true);
            _messageRepositoryMock
                .Setup(x => x.SearchMessagesAsync(conversationId, currentId, "blocked", 1, 20))
                .ReturnsAsync((new[] { systemMessage }, 1));
            // Act
            var result = await _conversationService.SearchMessagesAsync(conversationId, currentId, "blocked", 1, 20);

            // Assert
            result.Items.Should().HaveCount(1);
            var messageResult = result.Items.Single();
            messageResult.SystemMessageDataJson.Should().NotBeNullOrWhiteSpace();

            using var payload = JsonDocument.Parse(messageResult.SystemMessageDataJson!);
            payload.RootElement
                .GetProperty("targetUsernames")[0]
                .GetString()
                .Should()
                .Be("blocked-user");
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
        public async Task CreatePrivateConversationAsync_SameUser_ThrowsNotFoundException_WhenControllerValidationIsBypassed()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            var act = () => _conversationService.CreatePrivateConversationAsync(userId, userId);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("One or both accounts do not exist.");
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
            Dictionary<Guid, bool>? capturedMuteMap = null;
            SendMessageResponse? capturedRealtimeMessage = null;

            _accountRepositoryMock.Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { creator, member1, member2 });
            _followRepositoryMock.Setup(x => x.GetConnectedAccountIdsAsync(currentId, It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new HashSet<Guid>());
            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new Dictionary<Guid, bool>
                {
                    { currentId, false },
                    { member1Id, false },
                    { member2Id, false }
                });
            _realtimeServiceMock
                .Setup(x => x.NotifyNewMessageAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Dictionary<Guid, bool>>(),
                    It.IsAny<SendMessageResponse>()))
                .Callback<Guid, Dictionary<Guid, bool>, SendMessageResponse>((_, muteMap, realtimeMessage) =>
                {
                    capturedMuteMap = muteMap;
                    capturedRealtimeMessage = realtimeMessage;
                })
                .Returns(Task.CompletedTask);

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

            _realtimeServiceMock.Verify(x => x.NotifyNewMessageAsync(
                capturedConversation.ConversationId,
                It.IsAny<Dictionary<Guid, bool>>(),
                It.IsAny<SendMessageResponse>()), Times.Once);
            capturedMuteMap.Should().NotBeNull();
            capturedMuteMap!.Keys.Should().Contain(new[] { currentId, member1Id, member2Id });
            capturedRealtimeMessage.Should().NotBeNull();
            capturedRealtimeMessage!.Sender.Should().NotBeNull();
            capturedRealtimeMessage.Sender.AccountId.Should().Be(currentId);
            capturedRealtimeMessage.Sender.Username.Should().Be("creator");
        }

        [Fact]
        public async Task CreateGroupConversationAsync_WhenBlockedRelationExists_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var member1Id = Guid.NewGuid();
            var member2Id = Guid.NewGuid();
            var request = new CreateGroupConversationRequest
            {
                GroupName = "Blocked Group",
                MemberIds = new List<Guid> { member1Id, member2Id }
            };

            _accountBlockRepositoryMock
                .Setup(x => x.GetRelationsAsync(
                    currentId,
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<AccountBlockRelationModel>
                {
                    new()
                    {
                        TargetId = member1Id,
                        IsBlockedByCurrentUser = true
                    }
                });

            // Act
            var act = () => _conversationService.CreateGroupConversationAsync(currentId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("*selected members are unavailable*");
            _accountRepositoryMock.Verify(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()), Times.Never);
            _conversationRepositoryMock.Verify(x => x.AddConversationAsync(It.IsAny<Conversation>()), Times.Never);
        }

        [Fact]
        public async Task CreateGroupConversationAsync_GroupNameTooLong_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var request = new CreateGroupConversationRequest
            {
                GroupName = new string('a', 51),
                MemberIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
            };

            // Act
            var act = () => _conversationService.CreateGroupConversationAsync(currentId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("*at most 50 characters*");
        }

        [Fact]
        public async Task CreateGroupConversationAsync_TotalMembersLessThanMinimum_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var request = new CreateGroupConversationRequest
            {
                GroupName = "New Group",
                MemberIds = new List<Guid> { Guid.NewGuid() }
            };

            // Act
            var act = () => _conversationService.CreateGroupConversationAsync(currentId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("*at least 3 members*");
        }

        [Fact]
        public async Task CreateGroupConversationAsync_TotalMembersExceedsMaximum_ThrowsBadRequestException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var request = new CreateGroupConversationRequest
            {
                GroupName = "Big Group",
                MemberIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList()
            };

            // Act
            var act = () => _conversationService.CreateGroupConversationAsync(currentId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("*at most 50 members*");
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

        #region UpdateGroupConversationInfoAsync Tests

        [Fact]
        public async Task UpdateGroupConversationInfoAsync_NotMember_ThrowsForbiddenException()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, currentId))
                .ReturnsAsync(false);

            var request = new UpdateGroupConversationRequest
            {
                ConversationName = "New Group Name"
            };

            // Act
            var act = () => _conversationService.UpdateGroupConversationInfoAsync(conversationId, currentId, request);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You are not a member of this conversation.");
        }

        [Fact]
        public async Task UpdateGroupConversationInfoAsync_ValidNameAndAvatar_UpdatesConversationAndDeletesOldAvatar()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var oldAvatarUrl = "https://res.cloudinary.com/demo/image/upload/v1/old-avatar.png";
            var newAvatarUrl = "https://res.cloudinary.com/demo/image/upload/v2/new-avatar.png";
            var oldPublicId = "cloudmCloudM/images/old-avatar";
            var actor = new Account
            {
                AccountId = currentId,
                Username = "actor",
                FullName = "Actor",
                Status = AccountStatusEnum.Active
            };

            var conversation = new Conversation
            {
                ConversationId = conversationId,
                ConversationName = "Old Name",
                ConversationAvatar = oldAvatarUrl,
                IsGroup = true
            };

            var avatarFileMock = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
            avatarFileMock.Setup(x => x.Length).Returns(128);

            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, currentId))
                .ReturnsAsync(true);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMemberAsync(conversationId, currentId))
                .ReturnsAsync(new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    IsAdmin = true,
                    HasLeft = false
                });

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(currentId))
                .ReturnsAsync(actor);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(conversationId))
                .ReturnsAsync(new Dictionary<Guid, bool>
                {
                    [currentId] = false
                });

            _cloudinaryServiceMock
                .Setup(x => x.UploadImageAsync(avatarFileMock.Object))
                .ReturnsAsync(newAvatarUrl);

            _cloudinaryServiceMock
                .Setup(x => x.GetPublicIdFromUrl(oldAvatarUrl))
                .Returns(oldPublicId);

            _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<bool>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<bool>> operation, Func<Task>? _) => operation());

            var request = new UpdateGroupConversationRequest
            {
                ConversationName = "New Name",
                ConversationAvatar = avatarFileMock.Object
            };

            // Act
            await _conversationService.UpdateGroupConversationInfoAsync(conversationId, currentId, request);

            // Assert
            conversation.ConversationName.Should().Be("New Name");
            conversation.ConversationAvatar.Should().Be(newAvatarUrl);
            _conversationRepositoryMock.Verify(x => x.UpdateConversationAsync(It.IsAny<Conversation>()), Times.Once);
            _messageRepositoryMock.Verify(x => x.AddMessageAsync(It.IsAny<Message>()), Times.Once);
            _cloudinaryServiceMock.Verify(x => x.DeleteMediaAsync(oldPublicId, MediaTypeEnum.Image), Times.Once);
            _realtimeServiceMock.Verify(x => x.NotifyGroupConversationInfoUpdatedAsync(
                conversationId,
                "New Name",
                newAvatarUrl,
                It.Is<Guid?>(owner => owner == null),
                currentId), Times.Once);
        }

        [Fact]
        public async Task UpdateGroupConversationInfoAsync_TransactionFails_DeletesNewUploadedAvatarOnRollback()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var oldAvatarUrl = "https://res.cloudinary.com/demo/image/upload/v1/old-avatar.png";
            var newAvatarUrl = "https://res.cloudinary.com/demo/image/upload/v2/new-avatar.png";
            var newPublicId = "cloudmCloudM/images/new-avatar";
            var actor = new Account
            {
                AccountId = currentId,
                Username = "actor",
                FullName = "Actor",
                Status = AccountStatusEnum.Active
            };

            var conversation = new Conversation
            {
                ConversationId = conversationId,
                ConversationName = "Old Name",
                ConversationAvatar = oldAvatarUrl,
                IsGroup = true
            };

            var avatarFileMock = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
            avatarFileMock.Setup(x => x.Length).Returns(128);

            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, currentId))
                .ReturnsAsync(true);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMemberAsync(conversationId, currentId))
                .ReturnsAsync(new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    IsAdmin = true,
                    HasLeft = false
                });

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(currentId))
                .ReturnsAsync(actor);

            _cloudinaryServiceMock
                .Setup(x => x.UploadImageAsync(avatarFileMock.Object))
                .ReturnsAsync(newAvatarUrl);

            _cloudinaryServiceMock
                .Setup(x => x.GetPublicIdFromUrl(newAvatarUrl))
                .Returns(newPublicId);

            _conversationRepositoryMock
                .Setup(x => x.UpdateConversationAsync(It.IsAny<Conversation>()))
                .ThrowsAsync(new Exception("DB failure"));

            _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<bool>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns(async (Func<Task<bool>> operation, Func<Task>? onRollback) =>
                {
                    try
                    {
                        return await operation();
                    }
                    catch
                    {
                        if (onRollback != null)
                        {
                            await onRollback();
                        }

                        throw;
                    }
                });

            var request = new UpdateGroupConversationRequest
            {
                ConversationAvatar = avatarFileMock.Object
            };

            // Act
            var act = () => _conversationService.UpdateGroupConversationInfoAsync(conversationId, currentId, request);

            // Assert
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("DB failure");
            _cloudinaryServiceMock.Verify(x => x.DeleteMediaAsync(newPublicId, MediaTypeEnum.Image), Times.Once);
        }

        [Fact]
        public async Task UpdateGroupConversationInfoAsync_RemoveAvatarOnly_UpdatesConversationAndDeletesOldAvatar()
        {
            // Arrange
            var currentId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var oldAvatarUrl = "https://res.cloudinary.com/demo/image/upload/v1/old-avatar.png";
            var oldPublicId = "cloudmCloudM/images/old-avatar";
            var actor = new Account
            {
                AccountId = currentId,
                Username = "actor",
                FullName = "Actor",
                Status = AccountStatusEnum.Active
            };

            var conversation = new Conversation
            {
                ConversationId = conversationId,
                ConversationName = "Old Name",
                ConversationAvatar = oldAvatarUrl,
                IsGroup = true
            };

            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, currentId))
                .ReturnsAsync(true);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMemberAsync(conversationId, currentId))
                .ReturnsAsync(new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    IsAdmin = true,
                    HasLeft = false
                });
            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(currentId))
                .ReturnsAsync(actor);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(conversationId))
                .ReturnsAsync(new Dictionary<Guid, bool>
                {
                    [currentId] = false
                });
            _cloudinaryServiceMock
                .Setup(x => x.GetPublicIdFromUrl(oldAvatarUrl))
                .Returns(oldPublicId);
            _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<bool>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<bool>> operation, Func<Task>? _) => operation());

            var request = new UpdateGroupConversationRequest
            {
                RemoveAvatar = true
            };

            // Act
            await _conversationService.UpdateGroupConversationInfoAsync(conversationId, currentId, request);

            // Assert
            conversation.ConversationAvatar.Should().BeNull();
            _conversationRepositoryMock.Verify(x => x.UpdateConversationAsync(It.IsAny<Conversation>()), Times.Once);
            _cloudinaryServiceMock.Verify(x => x.DeleteMediaAsync(oldPublicId, MediaTypeEnum.Image), Times.Once);
            _realtimeServiceMock.Verify(x => x.NotifyGroupConversationInfoUpdatedAsync(
                conversationId,
                "Old Name",
                null,
                It.Is<Guid?>(owner => owner == null),
                currentId), Times.Once);
        }

        #endregion
    }
}
