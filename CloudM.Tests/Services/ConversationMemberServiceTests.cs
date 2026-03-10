using AutoMapper;
using FluentAssertions;
using Moq;
using CloudM.Application.DTOs.ConversationDTOs;
using CloudM.Application.DTOs.MessageDTOs;
using CloudM.Application.Services.ConversationMemberServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.ConversationMembers;
using CloudM.Infrastructure.Repositories.Conversations;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.Messages;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class ConversationMemberServiceTests
    {
        private readonly Mock<IConversationRepository> _conversationRepositoryMock;
        private readonly Mock<IConversationMemberRepository> _conversationMemberRepositoryMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IFollowRepository> _followRepositoryMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IMessageRepository> _messageRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IRealtimeService> _realtimeServiceMock;
        private readonly ConversationMemberService _service;

        public ConversationMemberServiceTests()
        {
            _conversationRepositoryMock = new Mock<IConversationRepository>();
            _conversationMemberRepositoryMock = new Mock<IConversationMemberRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _followRepositoryMock = new Mock<IFollowRepository>();
            _mapperMock = new Mock<IMapper>();
            _messageRepositoryMock = new Mock<IMessageRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _realtimeServiceMock = new Mock<IRealtimeService>();

            _service = new ConversationMemberService(
                _conversationRepositoryMock.Object,
                _conversationMemberRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _followRepositoryMock.Object,
                _mapperMock.Object,
                _messageRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _realtimeServiceMock.Object);

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<bool>>>(), It.IsAny<Func<Task>?>()))
                .Returns<Func<Task<bool>>, Func<Task>?>((operation, _) => operation());
        }

        [Fact]
        public async Task SearchAccountsForAddGroupMembersAsync_GroupAlreadyAtCapacityWithLegacyMembers_ReturnsEmpty()
        {
            // arrange
            var conversationId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var conversation = new Conversation
            {
                ConversationId = conversationId,
                IsGroup = true,
                Owner = currentId,
                CreatedBy = currentId
            };

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMemberAsync(conversationId, currentId))
                .ReturnsAsync(new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    IsAdmin = true,
                    HasLeft = false
                });
            _conversationMemberRepositoryMock
                .Setup(x => x.GetAllActiveMemberIdsByConversationIdAsync(conversationId))
                .ReturnsAsync(Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList());

            // act
            var result = await _service.SearchAccountsForAddGroupMembersAsync(
                conversationId,
                currentId,
                "user",
                null,
                10);

            // assert
            result.Should().BeEmpty();
            _accountRepositoryMock.Verify(
                x => x.SearchAccountsForGroupInviteAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<int>()),
                Times.Never);
        }

        [Fact]
        public async Task LeaveGroupAsync_OwnerStillHasLegacyActiveMembers_ThrowsTransferRequired()
        {
            // arrange
            var conversationId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var legacyAdminId = Guid.NewGuid();
            var conversation = new Conversation
            {
                ConversationId = conversationId,
                IsGroup = true,
                Owner = currentId,
                CreatedBy = currentId
            };

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMemberAsync(conversationId, currentId))
                .ReturnsAsync(new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    IsAdmin = true,
                    HasLeft = false
                });
            _conversationMemberRepositoryMock
                .Setup(x => x.GetAllActiveConversationMembersAsync(conversationId))
                .ReturnsAsync(new List<ConversationMember>
                {
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = currentId,
                        HasLeft = false,
                        Account = new Account
                        {
                            AccountId = currentId,
                            Username = "owner",
                            Status = AccountStatusEnum.Active,
                            RoleId = (int)RoleEnum.User
                        }
                    },
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = legacyAdminId,
                        HasLeft = false,
                        Account = new Account
                        {
                            AccountId = legacyAdminId,
                            Username = "admin",
                            Status = AccountStatusEnum.Active,
                            RoleId = (int)RoleEnum.Admin
                        }
                    }
                });

            // act
            var action = () => _service.LeaveGroupAsync(conversationId, currentId);

            // assert
            await action.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Group owner must transfer ownership before leaving the group.");
        }

        [Fact]
        public async Task KickGroupMemberAsync_LegacySystemMember_AllowsOwnerCleanup()
        {
            // arrange
            var conversationId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var targetAccountId = Guid.NewGuid();
            var conversation = new Conversation
            {
                ConversationId = conversationId,
                IsGroup = true,
                Owner = currentId,
                CreatedBy = currentId
            };
            var actorMember = new ConversationMember
            {
                ConversationId = conversationId,
                AccountId = currentId,
                IsAdmin = true,
                HasLeft = false
            };
            var targetMember = new ConversationMember
            {
                ConversationId = conversationId,
                AccountId = targetAccountId,
                IsAdmin = true,
                HasLeft = false
            };
            var actor = new Account
            {
                AccountId = currentId,
                Username = "owner",
                Status = AccountStatusEnum.Active,
                RoleId = (int)RoleEnum.User
            };
            var legacyTarget = new Account
            {
                AccountId = targetAccountId,
                Username = "legacy-admin",
                Status = AccountStatusEnum.Active,
                RoleId = (int)RoleEnum.Admin
            };

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMemberAsync(conversationId, currentId))
                .ReturnsAsync(actorMember);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMemberAsync(conversationId, targetAccountId))
                .ReturnsAsync(targetMember);
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(currentId))
                .ReturnsAsync(actor);
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(targetAccountId))
                .ReturnsAsync(legacyTarget);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(conversationId))
                .ReturnsAsync(new Dictionary<Guid, bool>());
            _mapperMock
                .Setup(x => x.Map<SendMessageResponse>(It.IsAny<Message>()))
                .Returns(new SendMessageResponse());

            // act
            var action = () => _service.KickGroupMemberAsync(conversationId, currentId, targetAccountId);

            // assert
            await action.Should().NotThrowAsync();
            targetMember.HasLeft.Should().BeTrue();
            targetMember.IsAdmin.Should().BeFalse();
            _conversationMemberRepositoryMock.Verify(x => x.UpdateConversationMember(targetMember), Times.Once);
            _realtimeServiceMock.Verify(
                x => x.NotifyConversationRemovedAsync(targetAccountId, conversationId, "kicked"),
                Times.Once);
        }

        [Fact]
        public async Task TransferGroupOwnerAsync_LegacySystemMember_ThrowsBadRequest()
        {
            // arrange
            var conversationId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var targetAccountId = Guid.NewGuid();
            var conversation = new Conversation
            {
                ConversationId = conversationId,
                IsGroup = true,
                Owner = currentId,
                CreatedBy = currentId
            };

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMemberAsync(conversationId, currentId))
                .ReturnsAsync(new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = currentId,
                    IsAdmin = true,
                    HasLeft = false
                });
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMemberAsync(conversationId, targetAccountId))
                .ReturnsAsync(new ConversationMember
                {
                    ConversationId = conversationId,
                    AccountId = targetAccountId,
                    IsAdmin = false,
                    HasLeft = false
                });
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(currentId))
                .ReturnsAsync(new Account
                {
                    AccountId = currentId,
                    Username = "owner",
                    Status = AccountStatusEnum.Active,
                    RoleId = (int)RoleEnum.User
                });
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(targetAccountId))
                .ReturnsAsync(new Account
                {
                    AccountId = targetAccountId,
                    Username = "legacy-admin",
                    Status = AccountStatusEnum.Active,
                    RoleId = (int)RoleEnum.Admin
                });

            // act
            var action = () => _service.TransferGroupOwnerAsync(conversationId, currentId, targetAccountId);

            // assert
            await action.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Target account is not available for social group management.");
        }
    }
}
