using FluentAssertions;
using Moq;
using CloudM.Application.Services.MessageReactServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Infrastructure.Repositories.ConversationMembers;
using CloudM.Infrastructure.Repositories.Conversations;
using CloudM.Infrastructure.Repositories.MessageReacts;
using CloudM.Infrastructure.Repositories.Messages;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class MessageReactServiceTests
    {
        private readonly Mock<IMessageRepository> _messageRepositoryMock;
        private readonly Mock<IMessageReactRepository> _messageReactRepositoryMock;
        private readonly Mock<IConversationMemberRepository> _conversationMemberRepositoryMock;
        private readonly Mock<IConversationRepository> _conversationRepositoryMock;
        private readonly Mock<IAccountBlockRepository> _accountBlockRepositoryMock;
        private readonly Mock<IRealtimeService> _realtimeServiceMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly MessageReactService _messageReactService;

        public MessageReactServiceTests()
        {
            _messageRepositoryMock = new Mock<IMessageRepository>();
            _messageReactRepositoryMock = new Mock<IMessageReactRepository>();
            _conversationMemberRepositoryMock = new Mock<IConversationMemberRepository>();
            _conversationRepositoryMock = new Mock<IConversationRepository>();
            _accountBlockRepositoryMock = new Mock<IAccountBlockRepository>();
            _realtimeServiceMock = new Mock<IRealtimeService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _messageReactService = new MessageReactService(
                _messageRepositoryMock.Object,
                _messageReactRepositoryMock.Object,
                _conversationMemberRepositoryMock.Object,
                _conversationRepositoryMock.Object,
                _realtimeServiceMock.Object,
                _unitOfWorkMock.Object,
                _accountBlockRepositoryMock.Object
            );

            _accountBlockRepositoryMock
                .Setup(x => x.IsBlockedEitherWayAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(false);
        }

        [Fact]
        public async Task SetMessageReactAsync_NoExistingReact_AddsReactAndReturnsUpdatedState()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var message = TestDataFactory.CreateMessage(messageId: messageId, conversationId: conversationId);
            var account = TestDataFactory.CreateAccount(accountId: accountId);

            _messageRepositoryMock.Setup(x => x.GetMessageByIdAsync(messageId))
                .ReturnsAsync(message);
            _conversationRepositoryMock.Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(TestDataFactory.CreateConversation(conversationId: conversationId, isGroup: true));
            _conversationMemberRepositoryMock.Setup(x => x.IsMemberOfConversation(conversationId, accountId))
                .ReturnsAsync(true);
            _conversationMemberRepositoryMock.Setup(x => x.GetConversationMembersAsync(conversationId))
                .ReturnsAsync(new List<ConversationMember>
                {
                    new ConversationMember
                    {
                        ConversationId = conversationId,
                        AccountId = accountId,
                        Nickname = "Me",
                        Account = account
                    }
                });
            _messageReactRepositoryMock.Setup(x => x.GetReactAsync(messageId, accountId))
                .ReturnsAsync((MessageReact?)null);
            _messageReactRepositoryMock.Setup(x => x.GetReactsByMessageIdAsync(messageId))
                .ReturnsAsync(new List<MessageReact>
                {
                    new MessageReact
                    {
                        MessageId = messageId,
                        AccountId = accountId,
                        ReactType = ReactEnum.Love,
                        CreatedAt = DateTime.UtcNow,
                        Account = account
                    }
                });

            // Act
            var result = await _messageReactService.SetMessageReactAsync(messageId, accountId, ReactEnum.Love);

            // Assert
            result.MessageId.Should().Be(messageId);
            result.ConversationId.Should().Be(conversationId);
            result.IsReacted.Should().BeTrue();
            result.CurrentUserReactType.Should().Be(ReactEnum.Love);
            result.TotalReacts.Should().Be(1);
            result.Reacts.Should().ContainSingle(x => x.ReactType == ReactEnum.Love && x.Count == 1);
            result.ReactedBy.Should().ContainSingle(x => x.AccountId == accountId && x.ReactType == ReactEnum.Love);

            _messageReactRepositoryMock.Verify(x => x.AddReactAsync(It.Is<MessageReact>(mr =>
                mr.MessageId == messageId &&
                mr.AccountId == accountId &&
                mr.ReactType == ReactEnum.Love)), Times.Once);
            _messageReactRepositoryMock.Verify(x => x.RemoveReactAsync(messageId, accountId), Times.Never);
            _messageReactRepositoryMock.Verify(x => x.UpdateReactAsync(It.IsAny<MessageReact>()), Times.Never);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
            _realtimeServiceMock.Verify(x => x.NotifyMessageReactUpdatedAsync(conversationId, messageId, accountId), Times.Once);
        }

        [Fact]
        public async Task SetMessageReactAsync_SameReactType_RemovesReact()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var message = TestDataFactory.CreateMessage(messageId: messageId, conversationId: conversationId);
            var account = TestDataFactory.CreateAccount(accountId: accountId);
            var existingReact = new MessageReact
            {
                MessageId = messageId,
                AccountId = accountId,
                ReactType = ReactEnum.Haha,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                Account = account
            };

            _messageRepositoryMock.Setup(x => x.GetMessageByIdAsync(messageId))
                .ReturnsAsync(message);
            _conversationRepositoryMock.Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(TestDataFactory.CreateConversation(conversationId: conversationId, isGroup: true));
            _conversationMemberRepositoryMock.Setup(x => x.IsMemberOfConversation(conversationId, accountId))
                .ReturnsAsync(true);
            _conversationMemberRepositoryMock.Setup(x => x.GetConversationMembersAsync(conversationId))
                .ReturnsAsync(new List<ConversationMember>
                {
                    new ConversationMember
                    {
                        ConversationId = conversationId,
                        AccountId = accountId,
                        Nickname = "Me",
                        Account = account
                    }
                });
            _messageReactRepositoryMock.Setup(x => x.GetReactAsync(messageId, accountId))
                .ReturnsAsync(existingReact);
            _messageReactRepositoryMock.Setup(x => x.GetReactsByMessageIdAsync(messageId))
                .ReturnsAsync(new List<MessageReact>());

            // Act
            var result = await _messageReactService.SetMessageReactAsync(messageId, accountId, ReactEnum.Haha);

            // Assert
            result.IsReacted.Should().BeFalse();
            result.CurrentUserReactType.Should().BeNull();
            result.TotalReacts.Should().Be(0);
            result.Reacts.Should().BeEmpty();
            result.ReactedBy.Should().BeEmpty();

            _messageReactRepositoryMock.Verify(x => x.RemoveReactAsync(messageId, accountId), Times.Once);
            _messageReactRepositoryMock.Verify(x => x.AddReactAsync(It.IsAny<MessageReact>()), Times.Never);
            _messageReactRepositoryMock.Verify(x => x.UpdateReactAsync(It.IsAny<MessageReact>()), Times.Never);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
            _realtimeServiceMock.Verify(x => x.NotifyMessageReactUpdatedAsync(conversationId, messageId, accountId), Times.Once);
        }

        [Fact]
        public async Task SetMessageReactAsync_DifferentReactType_UpdatesReact()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var message = TestDataFactory.CreateMessage(messageId: messageId, conversationId: conversationId);
            var account = TestDataFactory.CreateAccount(accountId: accountId);
            var existingReact = new MessageReact
            {
                MessageId = messageId,
                AccountId = accountId,
                ReactType = ReactEnum.Like,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                Account = account
            };

            _messageRepositoryMock.Setup(x => x.GetMessageByIdAsync(messageId))
                .ReturnsAsync(message);
            _conversationRepositoryMock.Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(TestDataFactory.CreateConversation(conversationId: conversationId, isGroup: true));
            _conversationMemberRepositoryMock.Setup(x => x.IsMemberOfConversation(conversationId, accountId))
                .ReturnsAsync(true);
            _conversationMemberRepositoryMock.Setup(x => x.GetConversationMembersAsync(conversationId))
                .ReturnsAsync(new List<ConversationMember>
                {
                    new ConversationMember
                    {
                        ConversationId = conversationId,
                        AccountId = accountId,
                        Nickname = "Me",
                        Account = account
                    }
                });
            _messageReactRepositoryMock.Setup(x => x.GetReactAsync(messageId, accountId))
                .ReturnsAsync(existingReact);
            _messageReactRepositoryMock.Setup(x => x.GetReactsByMessageIdAsync(messageId))
                .ReturnsAsync(() => new List<MessageReact>
                {
                    new MessageReact
                    {
                        MessageId = messageId,
                        AccountId = accountId,
                        ReactType = existingReact.ReactType,
                        CreatedAt = existingReact.CreatedAt,
                        Account = account
                    }
                });

            // Act
            var result = await _messageReactService.SetMessageReactAsync(messageId, accountId, ReactEnum.Wow);

            // Assert
            result.IsReacted.Should().BeTrue();
            result.CurrentUserReactType.Should().Be(ReactEnum.Wow);
            result.TotalReacts.Should().Be(1);
            result.Reacts.Should().ContainSingle(x => x.ReactType == ReactEnum.Wow && x.Count == 1);

            _messageReactRepositoryMock.Verify(x => x.UpdateReactAsync(It.Is<MessageReact>(mr =>
                mr.MessageId == messageId &&
                mr.AccountId == accountId &&
                mr.ReactType == ReactEnum.Wow)), Times.Once);
            _messageReactRepositoryMock.Verify(x => x.AddReactAsync(It.IsAny<MessageReact>()), Times.Never);
            _messageReactRepositoryMock.Verify(x => x.RemoveReactAsync(messageId, accountId), Times.Never);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
            _realtimeServiceMock.Verify(x => x.NotifyMessageReactUpdatedAsync(conversationId, messageId, accountId), Times.Once);
        }

        [Fact]
        public async Task SetMessageReactAsync_WhenPrivateConversationIsBlocked_ThrowsBadRequestException()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var message = TestDataFactory.CreateMessage(messageId: messageId, conversationId: conversationId);

            _messageRepositoryMock.Setup(x => x.GetMessageByIdAsync(messageId))
                .ReturnsAsync(message);
            _conversationRepositoryMock.Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(TestDataFactory.CreateConversation(conversationId: conversationId, isGroup: false));
            _conversationMemberRepositoryMock.Setup(x => x.IsMemberOfConversation(conversationId, accountId))
                .ReturnsAsync(true);
            _conversationMemberRepositoryMock.Setup(x => x.GetAllActiveMemberIdsByConversationIdAsync(conversationId))
                .ReturnsAsync(new List<Guid> { accountId, otherId });
            _accountBlockRepositoryMock.Setup(x => x.IsBlockedEitherWayAsync(accountId, otherId))
                .ReturnsAsync(true);

            // Act
            var act = () => _messageReactService.SetMessageReactAsync(messageId, accountId, ReactEnum.Like);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("This conversation is unavailable.");
            _messageReactRepositoryMock.Verify(x => x.AddReactAsync(It.IsAny<MessageReact>()), Times.Never);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Never);
            _realtimeServiceMock.Verify(x => x.NotifyMessageReactUpdatedAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>()), Times.Never);
        }
    }
}
