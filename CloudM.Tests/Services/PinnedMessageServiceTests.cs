using AutoMapper;
using FluentAssertions;
using Moq;
using CloudM.Application.Services.PinnedMessageServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.ConversationMembers;
using CloudM.Infrastructure.Repositories.Conversations;
using CloudM.Infrastructure.Repositories.Messages;
using CloudM.Infrastructure.Repositories.PinnedMessages;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Tests.Helpers;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class PinnedMessageServiceTests
    {
        private readonly Mock<IPinnedMessageRepository> _pinnedMessageRepositoryMock;
        private readonly Mock<IMessageRepository> _messageRepositoryMock;
        private readonly Mock<IConversationRepository> _conversationRepositoryMock;
        private readonly Mock<IConversationMemberRepository> _conversationMemberRepositoryMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IRealtimeService> _realtimeServiceMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IAccountBlockRepository> _accountBlockRepositoryMock;
        private readonly PinnedMessageService _service;

        public PinnedMessageServiceTests()
        {
            _pinnedMessageRepositoryMock = new Mock<IPinnedMessageRepository>();
            _messageRepositoryMock = new Mock<IMessageRepository>();
            _conversationRepositoryMock = new Mock<IConversationRepository>();
            _conversationMemberRepositoryMock = new Mock<IConversationMemberRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _realtimeServiceMock = new Mock<IRealtimeService>();
            _mapperMock = new Mock<IMapper>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _accountBlockRepositoryMock = new Mock<IAccountBlockRepository>();

            _accountBlockRepositoryMock
                .Setup(x => x.IsBlockedEitherWayAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(false);

            _service = new PinnedMessageService(
                _pinnedMessageRepositoryMock.Object,
                _messageRepositoryMock.Object,
                _conversationRepositoryMock.Object,
                _conversationMemberRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _realtimeServiceMock.Object,
                _mapperMock.Object,
                _unitOfWorkMock.Object,
                _accountBlockRepositoryMock.Object);
        }

        [Fact]
        public async Task PinMessageAsync_WhenPrivateConversationIsBlocked_ThrowsBadRequestException()
        {
            // arrange
            var conversationId = Guid.NewGuid();
            var messageId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var conversation = TestDataFactory.CreateConversation(conversationId: conversationId, isGroup: false);
            var message = TestDataFactory.CreateMessage(messageId: messageId, conversationId: conversationId);
            message.MessageType = MessageTypeEnum.Text;

            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, currentId))
                .ReturnsAsync(true);
            _messageRepositoryMock
                .Setup(x => x.GetMessageByIdAsync(messageId))
                .ReturnsAsync(message);
            _pinnedMessageRepositoryMock
                .Setup(x => x.IsPinnedAsync(conversationId, messageId))
                .ReturnsAsync(false);
            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetAllActiveMemberIdsByConversationIdAsync(conversationId))
                .ReturnsAsync(new List<Guid> { currentId, otherId });
            _accountBlockRepositoryMock
                .Setup(x => x.IsBlockedEitherWayAsync(currentId, otherId))
                .ReturnsAsync(true);

            // act
            var act = () => _service.PinMessageAsync(conversationId, messageId, currentId);

            // assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("This conversation is unavailable.");
            _pinnedMessageRepositoryMock.Verify(x => x.AddAsync(It.IsAny<PinnedMessage>()), Times.Never);
            _messageRepositoryMock.Verify(x => x.AddMessageAsync(It.IsAny<Message>()), Times.Never);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Never);
        }
    }
}
