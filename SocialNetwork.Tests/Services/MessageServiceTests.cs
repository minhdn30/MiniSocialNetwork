using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using SocialNetwork.Application.Services.MessageServices;
using SocialNetwork.Application.Services.RealtimeServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.MessageMedias;
using SocialNetwork.Infrastructure.Repositories.Messages;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Tests.Helpers;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Tests.Services
{
    public class MessageServiceTests
    {
        private readonly Mock<IMessageRepository> _messageRepositoryMock;
        private readonly Mock<IMessageMediaRepository> _messageMediaRepositoryMock;
        private readonly Mock<IConversationRepository> _conversationRepositoryMock;
        private readonly Mock<IConversationMemberRepository> _conversationMemberRepositoryMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<ICloudinaryService> _cloudinaryServiceMock;
        private readonly Mock<IFileTypeDetector> _fileTypeDetectorMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IRealtimeService> _realtimeServiceMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly MessageService _messageService;

        public MessageServiceTests()
        {
            _messageRepositoryMock = new Mock<IMessageRepository>();
            _messageMediaRepositoryMock = new Mock<IMessageMediaRepository>();
            _conversationRepositoryMock = new Mock<IConversationRepository>();
            _conversationMemberRepositoryMock = new Mock<IConversationMemberRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _cloudinaryServiceMock = new Mock<ICloudinaryService>();
            _fileTypeDetectorMock = new Mock<IFileTypeDetector>();
            _mapperMock = new Mock<IMapper>();
            _realtimeServiceMock = new Mock<IRealtimeService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _messageService = new MessageService(
                _messageRepositoryMock.Object,
                _messageMediaRepositoryMock.Object,
                _conversationRepositoryMock.Object,
                _conversationMemberRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _mapperMock.Object,
                _accountRepositoryMock.Object,
                _cloudinaryServiceMock.Object,
                _fileTypeDetectorMock.Object,
                _realtimeServiceMock.Object,
                _unitOfWorkMock.Object
            );
        }

        #region GetMessagesByConversationIdAsync Tests

        [Fact]
        public async Task GetMessagesByConversationIdAsync_ValidMember_ReturnsPagedResponse()
        {
            // Arrange
            var conversationId = Guid.NewGuid();
            var currentId = Guid.NewGuid();
            var messages = new List<MessageBasicModel>
            {
                TestDataFactory.CreateMessageBasicModel()
            };

            _conversationMemberRepositoryMock.Setup(x => x.IsMemberOfConversation(conversationId, currentId))
                .ReturnsAsync(true);
            _messageRepositoryMock.Setup(x => x.GetMessagesByConversationId(conversationId, currentId, 1, 20))
                .Returns(Task.FromResult((messages.AsEnumerable(), 1)));

            // Act
            var result = await _messageService.GetMessagesByConversationIdAsync(conversationId, currentId, 1, 20);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.TotalItems.Should().Be(1);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(20);
        }

        [Fact]
        public async Task GetMessagesByConversationIdAsync_NotMember_ThrowsForbiddenException()
        {
            // Arrange
            var conversationId = Guid.NewGuid();
            var currentId = Guid.NewGuid();

            _conversationMemberRepositoryMock.Setup(x => x.IsMemberOfConversation(conversationId, currentId))
                .ReturnsAsync(false);

            // Act
            var act = () => _messageService.GetMessagesByConversationIdAsync(conversationId, currentId, 1, 20);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You are not a member of this conversation.");
        }

        #endregion

        #region SendMessageInPrivateChatAsync Tests

        [Fact]
        public async Task SendMessageInPrivateChatAsync_SameUser_ThrowsBadRequestException()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var request = new SendMessageInPrivateChatRequest
            {
                ReceiverId = senderId, // Same as sender
                Content = "Hello"
            };

            // Act
            var act = () => _messageService.SendMessageInPrivateChatAsync(senderId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("You cannot send a message to yourself.");
        }

        [Fact]
        public async Task SendMessageInPrivateChatAsync_EmptyContentNoMedia_ThrowsBadRequestException()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var request = new SendMessageInPrivateChatRequest
            {
                ReceiverId = Guid.NewGuid(),
                Content = "",
                MediaFiles = null
            };

            // Act
            var act = () => _messageService.SendMessageInPrivateChatAsync(senderId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Message content and media files cannot both be empty.");
        }

        [Fact]
        public async Task SendMessageInPrivateChatAsync_ReceiverNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var request = new SendMessageInPrivateChatRequest
            {
                ReceiverId = receiverId,
                Content = "Hello"
            };

            _accountRepositoryMock.Setup(x => x.GetAccountById(receiverId)).ReturnsAsync((Account?)null);

            // Act
            var act = () => _messageService.SendMessageInPrivateChatAsync(senderId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage($"Receiver account with ID {receiverId} does not exist.");
        }

        [Fact]
        public async Task SendMessageInPrivateChatAsync_ReceiverInactive_ThrowsBadRequestException()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var receiver = TestDataFactory.CreateAccount(accountId: receiverId, status: AccountStatusEnum.Banned);
            var request = new SendMessageInPrivateChatRequest
            {
                ReceiverId = receiverId,
                Content = "Hello"
            };

            _accountRepositoryMock.Setup(x => x.GetAccountById(receiverId)).ReturnsAsync(receiver);

            // Act
            var act = () => _messageService.SendMessageInPrivateChatAsync(senderId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("This user is currently unavailable.");
        }

        [Fact]
        public async Task SendMessageInPrivateChatAsync_SenderNotFound_ThrowsBadRequestException()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var receiver = TestDataFactory.CreateAccount(accountId: receiverId);
            var request = new SendMessageInPrivateChatRequest
            {
                ReceiverId = receiverId,
                Content = "Hello"
            };

            _accountRepositoryMock.Setup(x => x.GetAccountById(receiverId)).ReturnsAsync(receiver);
            _accountRepositoryMock.Setup(x => x.GetAccountById(senderId)).ReturnsAsync((Account?)null);

            // Act
            var act = () => _messageService.SendMessageInPrivateChatAsync(senderId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage($"Sender account with ID {senderId} does not exist.");
        }

        [Fact]
        public async Task SendMessageInPrivateChatAsync_SenderInactive_ThrowsForbiddenException()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var receiver = TestDataFactory.CreateAccount(accountId: receiverId);
            var sender = TestDataFactory.CreateAccount(accountId: senderId, status: AccountStatusEnum.Suspended);
            var request = new SendMessageInPrivateChatRequest
            {
                ReceiverId = receiverId,
                Content = "Hello"
            };

            _accountRepositoryMock.Setup(x => x.GetAccountById(receiverId)).ReturnsAsync(receiver);
            _accountRepositoryMock.Setup(x => x.GetAccountById(senderId)).ReturnsAsync(sender);

            // Act
            var act = () => _messageService.SendMessageInPrivateChatAsync(senderId, request);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You must reactivate your account to send messages.");
        }

        [Fact]
        public async Task SendMessageInPrivateChatAsync_ValidRequest_SendsMessageAndReturnsResponse()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var sender = TestDataFactory.CreateAccount(accountId: senderId);
            var receiver = TestDataFactory.CreateAccount(accountId: receiverId);
            var conversation = TestDataFactory.CreateConversation(conversationId: conversationId);
            var request = new SendMessageInPrivateChatRequest
            {
                ReceiverId = receiverId,
                Content = "Hello"
            };
            var expectedResponse = new SendMessageResponse
            {
                ConversationId = conversationId,
                Content = "Hello"
            };

            _accountRepositoryMock.Setup(x => x.GetAccountById(receiverId)).ReturnsAsync(receiver);
            _accountRepositoryMock.Setup(x => x.GetAccountById(senderId)).ReturnsAsync(sender);
            _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<SendMessageResponse>>>(),
                It.IsAny<Func<Task>?>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _messageService.SendMessageInPrivateChatAsync(senderId, request);

            // Assert
            result.Should().NotBeNull();
            result.ConversationId.Should().Be(conversationId);
            result.Content.Should().Be("Hello");
        }

        #endregion
    }
}
