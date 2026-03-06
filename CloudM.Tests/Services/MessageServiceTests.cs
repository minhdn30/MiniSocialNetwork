using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.MessageDTOs;
using CloudM.Application.Helpers.FileTypeHelpers;
using CloudM.Infrastructure.Services.Cloudinary;
using CloudM.Application.Services.MessageServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.ConversationMembers;
using CloudM.Infrastructure.Repositories.Conversations;
using CloudM.Infrastructure.Repositories.MessageMedias;
using CloudM.Infrastructure.Repositories.Messages;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.Stories;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Tests.Helpers;
using System.Text.Json;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class MessageServiceTests
    {
        private readonly Mock<IMessageRepository> _messageRepositoryMock;
        private readonly Mock<IMessageMediaRepository> _messageMediaRepositoryMock;
        private readonly Mock<IConversationRepository> _conversationRepositoryMock;
        private readonly Mock<IConversationMemberRepository> _conversationMemberRepositoryMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IPostRepository> _postRepositoryMock;
        private readonly Mock<ICloudinaryService> _cloudinaryServiceMock;
        private readonly Mock<IFileTypeDetector> _fileTypeDetectorMock;
        private readonly Mock<IStoryRepository> _storyRepositoryMock;
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
            _postRepositoryMock = new Mock<IPostRepository>();
            _cloudinaryServiceMock = new Mock<ICloudinaryService>();
            _fileTypeDetectorMock = new Mock<IFileTypeDetector>();
            _storyRepositoryMock = new Mock<IStoryRepository>();
            _mapperMock = new Mock<IMapper>();
            _realtimeServiceMock = new Mock<IRealtimeService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _messageService = new MessageService(
                _messageRepositoryMock.Object,
                _messageMediaRepositoryMock.Object,
                _conversationRepositoryMock.Object,
                _conversationMemberRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _postRepositoryMock.Object,
                _mapperMock.Object,
                _cloudinaryServiceMock.Object,
                _fileTypeDetectorMock.Object,
                _storyRepositoryMock.Object,
                _realtimeServiceMock.Object,
                _unitOfWorkMock.Object
            );
        }

        #region GetMessagesByConversationIdAsync Tests

        [Fact]
        public async Task GetMessagesByConversationIdAsync_ValidMember_ReturnsCursorResponse()
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
            _messageRepositoryMock.Setup(x => x.GetMessagesByConversationId(conversationId, currentId, null, 20))
                .Returns(Task.FromResult((
                    (IReadOnlyList<MessageBasicModel>)messages,
                    (string?)"1",
                    (string?)null,
                    true,
                    false
                )));

            // Act
            var result = await _messageService.GetMessagesByConversationIdAsync(conversationId, currentId, null, 20);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.HasMoreOlder.Should().BeTrue();
            result.HasMoreNewer.Should().BeFalse();
            result.OlderCursor.Should().Be("1");
            result.NewerCursor.Should().BeNull();
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
            var act = () => _messageService.GetMessagesByConversationIdAsync(conversationId, currentId, null, 20);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You are not a member of this conversation.");
        }

        #endregion

        #region SendMessageInPrivateChatAsync Tests

        [Fact]
        public async Task SendMessageInPrivateChatAsync_SameUser_WithoutControllerValidation_ThrowsReceiverNotFound()
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
                .WithMessage($"Receiver account with ID {senderId} does not exist.");
        }

        [Fact]
        public async Task SendMessageInPrivateChatAsync_EmptyContentNoMedia_WithoutControllerValidation_ThrowsReceiverNotFound()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var request = new SendMessageInPrivateChatRequest
            {
                ReceiverId = receiverId,
                Content = "",
                MediaFiles = null
            };

            // Act
            var act = () => _messageService.SendMessageInPrivateChatAsync(senderId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage($"Receiver account with ID {receiverId} does not exist.");
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

        #region SendMessageInGroupAsync Tests

        [Fact]
        public async Task SendMessageInGroupAsync_ValidMention_ShouldCanonicalizeContentAndNotifyMentionedMembers()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var mentionedId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            var sender = TestDataFactory.CreateAccount(accountId: senderId, username: "sender");
            var mentioned = TestDataFactory.CreateAccount(accountId: mentionedId, username: "alice");
            var conversation = TestDataFactory.CreateConversation(conversationId: conversationId, isGroup: true);

            var request = new SendMessageRequest
            {
                Content = "hello @alice and @unknown",
                TempId = "tmp-group-mention-1"
            };

            Message? createdMessage = null;
            IEnumerable<Guid>? notifiedMentionIds = null;

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, senderId))
                .ReturnsAsync(true);
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(senderId))
                .ReturnsAsync(sender);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMembersAsync(conversationId))
                .ReturnsAsync(new List<ConversationMember>
                {
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = senderId,
                        Account = sender,
                        HasLeft = false
                    },
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = mentionedId,
                        Account = mentioned,
                        HasLeft = false
                    }
                });

            _messageRepositoryMock
                .Setup(x => x.AddMessageAsync(It.IsAny<Message>()))
                .Callback<Message>(message => createdMessage = message)
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<SendMessageResponse>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<SendMessageResponse>> operation, Func<Task>? _) => operation());

            _mapperMock
                .Setup(x => x.Map<AccountChatInfoResponse>(sender))
                .Returns(new AccountChatInfoResponse
                {
                    AccountId = senderId,
                    Username = sender.Username,
                    FullName = sender.FullName,
                    AvatarUrl = sender.AvatarUrl,
                    IsActive = true
                });

            _mapperMock
                .Setup(x => x.Map<SendMessageResponse>(It.IsAny<Message>()))
                .Returns<Message>(message => new SendMessageResponse
                {
                    MessageId = message.MessageId,
                    ConversationId = message.ConversationId,
                    Content = message.Content,
                    MessageType = message.MessageType,
                    SentAt = message.SentAt
                });

            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(conversationId))
                .ReturnsAsync(new Dictionary<Guid, bool>
                {
                    { senderId, false },
                    { mentionedId, true }
                });

            _realtimeServiceMock
                .Setup(x => x.NotifyNewMessageAsync(
                    conversationId,
                    It.IsAny<Dictionary<Guid, bool>>(),
                    It.IsAny<SendMessageResponse>(),
                    It.IsAny<IEnumerable<Guid>?>()))
                .Callback<Guid, Dictionary<Guid, bool>, SendMessageResponse, IEnumerable<Guid>?>((_, _, _, mentionIds) =>
                {
                    notifiedMentionIds = mentionIds;
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _messageService.SendMessageInGroupAsync(senderId, conversationId, request);

            // Assert
            var expectedCanonicalContent = $"hello @[alice]({mentionedId}) and @unknown";
            result.Content.Should().Be(expectedCanonicalContent);

            createdMessage.Should().NotBeNull();
            createdMessage!.Content.Should().Be(expectedCanonicalContent);

            notifiedMentionIds.Should().NotBeNull();
            notifiedMentionIds!.Should().ContainSingle(id => id == mentionedId);
            notifiedMentionIds.Should().NotContain(senderId);
        }

        [Fact]
        public async Task SendMessageInGroupAsync_HyphenContinuationMention_ShouldNotCreatePartialMention()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var memberId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            var sender = TestDataFactory.CreateAccount(accountId: senderId, username: "sender");
            var member = TestDataFactory.CreateAccount(accountId: memberId, username: "alice");
            var conversation = TestDataFactory.CreateConversation(conversationId: conversationId, isGroup: true);

            var request = new SendMessageRequest
            {
                Content = "hello @alice-zz",
                TempId = "tmp-group-mention-2"
            };

            Message? createdMessage = null;
            IEnumerable<Guid>? notifiedMentionIds = null;

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, senderId))
                .ReturnsAsync(true);
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(senderId))
                .ReturnsAsync(sender);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMembersAsync(conversationId))
                .ReturnsAsync(new List<ConversationMember>
                {
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = senderId,
                        Account = sender,
                        HasLeft = false
                    },
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = memberId,
                        Account = member,
                        HasLeft = false
                    }
                });

            _messageRepositoryMock
                .Setup(x => x.AddMessageAsync(It.IsAny<Message>()))
                .Callback<Message>(message => createdMessage = message)
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<SendMessageResponse>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<SendMessageResponse>> operation, Func<Task>? _) => operation());

            _mapperMock
                .Setup(x => x.Map<AccountChatInfoResponse>(sender))
                .Returns(new AccountChatInfoResponse
                {
                    AccountId = senderId,
                    Username = sender.Username,
                    FullName = sender.FullName,
                    AvatarUrl = sender.AvatarUrl,
                    IsActive = true
                });

            _mapperMock
                .Setup(x => x.Map<SendMessageResponse>(It.IsAny<Message>()))
                .Returns<Message>(message => new SendMessageResponse
                {
                    MessageId = message.MessageId,
                    ConversationId = message.ConversationId,
                    Content = message.Content,
                    MessageType = message.MessageType,
                    SentAt = message.SentAt
                });

            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(conversationId))
                .ReturnsAsync(new Dictionary<Guid, bool>
                {
                    { senderId, false },
                    { memberId, false }
                });

            _realtimeServiceMock
                .Setup(x => x.NotifyNewMessageAsync(
                    conversationId,
                    It.IsAny<Dictionary<Guid, bool>>(),
                    It.IsAny<SendMessageResponse>(),
                    It.IsAny<IEnumerable<Guid>?>()))
                .Callback<Guid, Dictionary<Guid, bool>, SendMessageResponse, IEnumerable<Guid>?>((_, _, _, mentionIds) =>
                {
                    notifiedMentionIds = mentionIds;
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _messageService.SendMessageInGroupAsync(senderId, conversationId, request);

            // Assert
            result.Content.Should().Be("hello @alice-zz");

            createdMessage.Should().NotBeNull();
            createdMessage!.Content.Should().Be("hello @alice-zz");

            notifiedMentionIds.Should().NotBeNull();
            notifiedMentionIds!.Should().BeEmpty();
        }

        [Fact]
        public async Task SendMessageInGroupAsync_AllMention_ShouldNotifyAllMembersExceptSender()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var memberAId = Guid.NewGuid();
            var memberBId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            var sender = TestDataFactory.CreateAccount(accountId: senderId, username: "sender");
            var memberA = TestDataFactory.CreateAccount(accountId: memberAId, username: "alice");
            var memberB = TestDataFactory.CreateAccount(accountId: memberBId, username: "bob");
            var conversation = TestDataFactory.CreateConversation(conversationId: conversationId, isGroup: true);

            var request = new SendMessageRequest
            {
                Content = "hello @all team",
                TempId = "tmp-group-mention-all-1"
            };

            Message? createdMessage = null;
            IEnumerable<Guid>? notifiedMentionIds = null;

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, senderId))
                .ReturnsAsync(true);
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(senderId))
                .ReturnsAsync(sender);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMembersAsync(conversationId))
                .ReturnsAsync(new List<ConversationMember>
                {
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = senderId,
                        Account = sender,
                        HasLeft = false
                    },
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = memberAId,
                        Account = memberA,
                        HasLeft = false
                    },
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = memberBId,
                        Account = memberB,
                        HasLeft = false
                    }
                });

            _messageRepositoryMock
                .Setup(x => x.AddMessageAsync(It.IsAny<Message>()))
                .Callback<Message>(message => createdMessage = message)
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<SendMessageResponse>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<SendMessageResponse>> operation, Func<Task>? _) => operation());

            _mapperMock
                .Setup(x => x.Map<AccountChatInfoResponse>(sender))
                .Returns(new AccountChatInfoResponse
                {
                    AccountId = senderId,
                    Username = sender.Username,
                    FullName = sender.FullName,
                    AvatarUrl = sender.AvatarUrl,
                    IsActive = true
                });

            _mapperMock
                .Setup(x => x.Map<SendMessageResponse>(It.IsAny<Message>()))
                .Returns<Message>(message => new SendMessageResponse
                {
                    MessageId = message.MessageId,
                    ConversationId = message.ConversationId,
                    Content = message.Content,
                    MessageType = message.MessageType,
                    SentAt = message.SentAt
                });

            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(conversationId))
                .ReturnsAsync(new Dictionary<Guid, bool>
                {
                    { senderId, false },
                    { memberAId, true },
                    { memberBId, false }
                });

            _realtimeServiceMock
                .Setup(x => x.NotifyNewMessageAsync(
                    conversationId,
                    It.IsAny<Dictionary<Guid, bool>>(),
                    It.IsAny<SendMessageResponse>(),
                    It.IsAny<IEnumerable<Guid>?>()))
                .Callback<Guid, Dictionary<Guid, bool>, SendMessageResponse, IEnumerable<Guid>?>((_, _, _, mentionIds) =>
                {
                    notifiedMentionIds = mentionIds;
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _messageService.SendMessageInGroupAsync(senderId, conversationId, request);

            // Assert
            result.Content.Should().Be("hello @all team");

            createdMessage.Should().NotBeNull();
            createdMessage!.Content.Should().Be("hello @all team");

            notifiedMentionIds.Should().NotBeNull();
            notifiedMentionIds!.Should().Contain(memberAId);
            notifiedMentionIds.Should().Contain(memberBId);
            notifiedMentionIds.Should().NotContain(senderId);
        }

        [Fact]
        public async Task SendMessageInGroupAsync_ConfiguredAllMentionKeyword_ShouldRespectConfiguredKeyword()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var memberAId = Guid.NewGuid();
            var memberBId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            var sender = TestDataFactory.CreateAccount(accountId: senderId, username: "sender");
            var memberA = TestDataFactory.CreateAccount(accountId: memberAId, username: "alice");
            var memberB = TestDataFactory.CreateAccount(accountId: memberBId, username: "bob");
            var conversation = TestDataFactory.CreateConversation(conversationId: conversationId, isGroup: true);

            var messageServiceWithCustomAllKeyword = new MessageService(
                _messageRepositoryMock.Object,
                _messageMediaRepositoryMock.Object,
                _conversationRepositoryMock.Object,
                _conversationMemberRepositoryMock.Object,
                _accountRepositoryMock.Object,
                _postRepositoryMock.Object,
                _mapperMock.Object,
                _cloudinaryServiceMock.Object,
                _fileTypeDetectorMock.Object,
                _storyRepositoryMock.Object,
                _realtimeServiceMock.Object,
                _unitOfWorkMock.Object,
                Options.Create(new ChatMentionOptions { GroupAllKeyword = "everyone" })
            );

            var request = new SendMessageRequest
            {
                Content = "hello @everyone team",
                TempId = "tmp-group-mention-all-2"
            };

            IEnumerable<Guid>? notifiedMentionIds = null;

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(conversation);
            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, senderId))
                .ReturnsAsync(true);
            _accountRepositoryMock
                .Setup(x => x.GetAccountById(senderId))
                .ReturnsAsync(sender);
            _conversationMemberRepositoryMock
                .Setup(x => x.GetConversationMembersAsync(conversationId))
                .ReturnsAsync(new List<ConversationMember>
                {
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = senderId,
                        Account = sender,
                        HasLeft = false
                    },
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = memberAId,
                        Account = memberA,
                        HasLeft = false
                    },
                    new()
                    {
                        ConversationId = conversationId,
                        AccountId = memberBId,
                        Account = memberB,
                        HasLeft = false
                    }
                });

            _messageRepositoryMock
                .Setup(x => x.AddMessageAsync(It.IsAny<Message>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<SendMessageResponse>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<SendMessageResponse>> operation, Func<Task>? _) => operation());

            _mapperMock
                .Setup(x => x.Map<AccountChatInfoResponse>(sender))
                .Returns(new AccountChatInfoResponse
                {
                    AccountId = senderId,
                    Username = sender.Username,
                    FullName = sender.FullName,
                    AvatarUrl = sender.AvatarUrl,
                    IsActive = true
                });

            _mapperMock
                .Setup(x => x.Map<SendMessageResponse>(It.IsAny<Message>()))
                .Returns<Message>(message => new SendMessageResponse
                {
                    MessageId = message.MessageId,
                    ConversationId = message.ConversationId,
                    Content = message.Content,
                    MessageType = message.MessageType,
                    SentAt = message.SentAt
                });

            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(conversationId))
                .ReturnsAsync(new Dictionary<Guid, bool>
                {
                    { senderId, false },
                    { memberAId, false },
                    { memberBId, false }
                });

            _realtimeServiceMock
                .Setup(x => x.NotifyNewMessageAsync(
                    conversationId,
                    It.IsAny<Dictionary<Guid, bool>>(),
                    It.IsAny<SendMessageResponse>(),
                    It.IsAny<IEnumerable<Guid>?>()))
                .Callback<Guid, Dictionary<Guid, bool>, SendMessageResponse, IEnumerable<Guid>?>((_, _, _, mentionIds) =>
                {
                    notifiedMentionIds = mentionIds;
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await messageServiceWithCustomAllKeyword.SendMessageInGroupAsync(senderId, conversationId, request);

            // Assert
            result.Content.Should().Be("hello @everyone team");
            notifiedMentionIds.Should().NotBeNull();
            notifiedMentionIds!.Should().Contain(memberAId);
            notifiedMentionIds.Should().Contain(memberBId);
            notifiedMentionIds.Should().NotContain(senderId);
        }

        #endregion

        #region SendStoryReplyAsync Tests

        [Fact]
        public async Task SendStoryReplyAsync_WhenStoryIsNotViewable_ThrowsBadRequestException()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var request = new SendStoryReplyRequest
            {
                ReceiverId = receiverId,
                StoryId = Guid.NewGuid(),
                Content = "reply"
            };
            var sender = TestDataFactory.CreateAccount(accountId: senderId);
            var receiver = TestDataFactory.CreateAccount(accountId: receiverId);

            _accountRepositoryMock
                .Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { sender, receiver });

            _storyRepositoryMock
                .Setup(x => x.GetViewableStoryByIdAsync(senderId, request.StoryId, It.IsAny<DateTime>()))
                .ReturnsAsync((Story?)null);

            // Act
            var act = () => _messageService.SendStoryReplyAsync(senderId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("This story is no longer available.");
        }

        [Fact]
        public async Task SendStoryReplyAsync_WhenReceiverIsNotStoryOwner_ThrowsBadRequestException()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var request = new SendStoryReplyRequest
            {
                ReceiverId = receiverId,
                StoryId = Guid.NewGuid(),
                Content = "reply"
            };
            var sender = TestDataFactory.CreateAccount(accountId: senderId);
            var receiver = TestDataFactory.CreateAccount(accountId: receiverId);
            var story = new Story
            {
                StoryId = request.StoryId,
                AccountId = ownerId,
                ContentType = StoryContentTypeEnum.Image,
                MediaUrl = "https://cdn.example.com/story.jpg",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                IsDeleted = false
            };

            _accountRepositoryMock
                .Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { sender, receiver });

            _storyRepositoryMock
                .Setup(x => x.GetViewableStoryByIdAsync(senderId, request.StoryId, It.IsAny<DateTime>()))
                .ReturnsAsync(story);

            // Act
            var act = () => _messageService.SendStoryReplyAsync(senderId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Story receiver does not match story owner.");
        }

        [Fact]
        public async Task SendStoryReplyAsync_UsesServerStorySnapshotInsteadOfClientPayload()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var sender = TestDataFactory.CreateAccount(accountId: senderId);
            var receiver = TestDataFactory.CreateAccount(accountId: receiverId);

            var story = new Story
            {
                StoryId = storyId,
                AccountId = receiverId,
                ContentType = StoryContentTypeEnum.Image,
                MediaUrl = "https://cdn.example.com/server-image.jpg",
                TextContent = null,
                BackgroundColorKey = "bg-server",
                TextColorKey = "text-server",
                FontTextKey = null,
                FontSizeKey = null,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3),
                ExpiresAt = DateTime.UtcNow.AddMinutes(20),
                IsDeleted = false
            };

            var request = new SendStoryReplyRequest
            {
                ReceiverId = receiverId,
                StoryId = storyId,
                Content = "nice story",
                TempId = "tmp-1",
                StoryMediaUrl = "https://spoofed-client-url",
                StoryContentType = (int)StoryContentTypeEnum.Text,
                StoryTextContent = "spoofed-text",
                StoryBackgroundColorKey = "spoof-bg",
                StoryTextColorKey = "spoof-text-color",
                StoryFontTextKey = "spoof-font",
                StoryFontSizeKey = "spoof-size"
            };

            Message? capturedMessage = null;

            _accountRepositoryMock
                .Setup(x => x.GetAccountsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Account> { sender, receiver });

            _storyRepositoryMock
                .Setup(x => x.GetViewableStoryByIdAsync(senderId, storyId, It.IsAny<DateTime>()))
                .ReturnsAsync(story);

            _conversationRepositoryMock
                .Setup(x => x.GetPrivateConversationIdAsync(senderId, receiverId))
                .ReturnsAsync(conversationId);

            _messageRepositoryMock
                .Setup(x => x.AddMessageAsync(It.IsAny<Message>()))
                .Callback<Message>(msg => capturedMessage = msg)
                .Returns(Task.CompletedTask);

            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(conversationId))
                .ReturnsAsync(new Dictionary<Guid, bool>
                {
                    { senderId, false },
                    { receiverId, false }
                });

            _realtimeServiceMock
                .Setup(x => x.NotifyNewMessageAsync(
                    conversationId,
                    It.IsAny<Dictionary<Guid, bool>>(),
                    It.IsAny<SendMessageResponse>()))
                .Returns(Task.CompletedTask);

            _mapperMock
                .Setup(x => x.Map<SendMessageResponse>(It.IsAny<Message>()))
                .Returns<Message>(msg => new SendMessageResponse
                {
                    MessageId = msg.MessageId,
                    ConversationId = msg.ConversationId,
                    Content = msg.Content,
                    MessageType = msg.MessageType,
                    SentAt = msg.SentAt
                });

            _mapperMock
                .Setup(x => x.Map<AccountChatInfoResponse>(It.IsAny<Account>()))
                .Returns<Account>(acc => new AccountChatInfoResponse
                {
                    AccountId = acc.AccountId,
                    Username = acc.Username,
                    FullName = acc.FullName,
                    AvatarUrl = acc.AvatarUrl,
                    IsActive = acc.Status == AccountStatusEnum.Active
                });

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<SendMessageResponse>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<SendMessageResponse>> operation, Func<Task>? _) => operation());

            // Act
            var result = await _messageService.SendStoryReplyAsync(senderId, request);

            // Assert
            capturedMessage.Should().NotBeNull();
            capturedMessage!.SystemMessageDataJson.Should().NotBeNullOrWhiteSpace();

            using var payload = JsonDocument.Parse(capturedMessage.SystemMessageDataJson!);
            payload.RootElement.GetProperty("storyId").GetGuid().Should().Be(storyId);
            payload.RootElement.GetProperty("mediaUrl").GetString().Should().Be(story.MediaUrl);
            payload.RootElement.GetProperty("contentType").GetInt32().Should().Be((int)story.ContentType);
            payload.RootElement.GetProperty("backgroundColorKey").GetString().Should().Be(story.BackgroundColorKey);
            payload.RootElement.GetProperty("textColorKey").GetString().Should().Be(story.TextColorKey);

            result.StoryReplyInfo.Should().NotBeNull();
            result.StoryReplyInfo!.MediaUrl.Should().Be(story.MediaUrl);
            result.StoryReplyInfo.ContentType.Should().Be((int)story.ContentType);
            result.StoryReplyInfo.BackgroundColorKey.Should().Be(story.BackgroundColorKey);
            result.StoryReplyInfo.TextColorKey.Should().Be(story.TextColorKey);
            result.StoryReplyInfo.FontTextKey.Should().Be(story.FontTextKey);
            result.StoryReplyInfo.FontSizeKey.Should().Be(story.FontSizeKey);

            result.StoryReplyInfo.MediaUrl.Should().NotBe(request.StoryMediaUrl);
            result.StoryReplyInfo.ContentType.Should().NotBe(request.StoryContentType);
        }

        #endregion

        #region SendPostShareAsync Tests

        [Fact]
        public async Task SendPostShareAsync_DuplicateTargetAfterFailedConversationShare_ShouldKeepFailedStatus()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            var sender = TestDataFactory.CreateAccount(accountId: senderId);
            var receiver = TestDataFactory.CreateAccount(accountId: receiverId);
            var post = new PostDetailModel
            {
                PostId = postId,
                PostCode = "POST-123",
                Owner = TestDataFactory.CreateAccountBasicInfoModel(Guid.NewGuid()),
                Content = "Post content"
            };

            _accountRepositoryMock.Setup(x => x.GetAccountById(senderId)).ReturnsAsync(sender);
            _accountRepositoryMock
                .Setup(x => x.GetAccountsByIds(It.Is<IEnumerable<Guid>>(ids => ids.Contains(receiverId))))
                .ReturnsAsync(new List<Account> { receiver });
            _postRepositoryMock.Setup(x => x.GetPostDetailByPostId(postId, senderId)).ReturnsAsync(post);
            _mapperMock.Setup(x => x.Map<AccountChatInfoResponse>(sender)).Returns(new AccountChatInfoResponse
            {
                AccountId = senderId,
                Username = sender.Username,
                FullName = sender.FullName,
                AvatarUrl = sender.AvatarUrl,
                IsActive = true
            });

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(TestDataFactory.CreateConversation(conversationId));
            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, senderId))
                .ReturnsAsync(false);
            _conversationRepositoryMock
                .Setup(x => x.GetPrivateConversationIdAsync(senderId, receiverId))
                .ReturnsAsync(conversationId);

            var request = new SendPostShareRequest
            {
                PostId = postId,
                ConversationIds = new List<Guid> { conversationId },
                ReceiverIds = new List<Guid> { receiverId }
            };

            // Act
            var result = await _messageService.SendPostShareAsync(senderId, request);

            // Assert
            result.TotalRequested.Should().Be(2);
            result.TotalSucceeded.Should().Be(0);
            result.TotalFailed.Should().Be(2);
            result.Results.Should().HaveCount(2);
            result.Results.Should().OnlyContain(item => !item.IsSuccess);
            result.Results
                .Where(item => item.ConversationId == conversationId)
                .Select(item => item.ErrorMessage)
                .Should()
                .OnlyContain(message => message == "You are not a member of this conversation.");
        }

        [Fact]
        public async Task SendPostShareAsync_DuplicateTargetAfterSuccessfulConversationShare_ShouldNotSendTwice()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            var sender = TestDataFactory.CreateAccount(accountId: senderId);
            var receiver = TestDataFactory.CreateAccount(accountId: receiverId);
            var post = new PostDetailModel
            {
                PostId = postId,
                PostCode = "POST-ABC",
                Owner = TestDataFactory.CreateAccountBasicInfoModel(Guid.NewGuid()),
                Content = "Post content"
            };

            _accountRepositoryMock.Setup(x => x.GetAccountById(senderId)).ReturnsAsync(sender);
            _accountRepositoryMock
                .Setup(x => x.GetAccountsByIds(It.Is<IEnumerable<Guid>>(ids => ids.Contains(receiverId))))
                .ReturnsAsync(new List<Account> { receiver });
            _postRepositoryMock.Setup(x => x.GetPostDetailByPostId(postId, senderId)).ReturnsAsync(post);

            _mapperMock.Setup(x => x.Map<AccountChatInfoResponse>(sender)).Returns(new AccountChatInfoResponse
            {
                AccountId = senderId,
                Username = sender.Username,
                FullName = sender.FullName,
                AvatarUrl = sender.AvatarUrl,
                IsActive = true
            });

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(TestDataFactory.CreateConversation(conversationId));
            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(conversationId, senderId))
                .ReturnsAsync(true);
            _conversationRepositoryMock
                .Setup(x => x.GetPrivateConversationIdAsync(senderId, receiverId))
                .ReturnsAsync(conversationId);
            _messageRepositoryMock
                .Setup(x => x.AddMessageAsync(It.IsAny<Message>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<SendMessageResponse>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<SendMessageResponse>> operation, Func<Task>? _) => operation());

            _mapperMock
                .Setup(x => x.Map<SendMessageResponse>(It.IsAny<Message>()))
                .Returns<Message>(message => new SendMessageResponse
                {
                    MessageId = message.MessageId,
                    ConversationId = message.ConversationId,
                    MessageType = message.MessageType,
                    SentAt = message.SentAt
                });

            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(conversationId))
                .ReturnsAsync(new Dictionary<Guid, bool> { { senderId, false }, { receiverId, false } });
            _realtimeServiceMock
                .Setup(x => x.NotifyNewMessageAsync(
                    conversationId,
                    It.IsAny<Dictionary<Guid, bool>>(),
                    It.IsAny<SendMessageResponse>()))
                .Returns(Task.CompletedTask);

            var request = new SendPostShareRequest
            {
                PostId = postId,
                ConversationIds = new List<Guid> { conversationId },
                ReceiverIds = new List<Guid> { receiverId }
            };

            // Act
            var result = await _messageService.SendPostShareAsync(senderId, request);

            // Assert
            result.TotalRequested.Should().Be(2);
            result.TotalSucceeded.Should().Be(2);
            result.TotalFailed.Should().Be(0);
            result.Results.Should().HaveCount(2);

            var successfulResults = result.Results
                .Where(item => item.ConversationId == conversationId && item.IsSuccess)
                .ToList();
            successfulResults.Should().HaveCount(2);
            successfulResults.Should().OnlyContain(item => item.Message != null);

            _messageRepositoryMock.Verify(x => x.AddMessageAsync(It.IsAny<Message>()), Times.Once);
            _realtimeServiceMock.Verify(x => x.NotifyNewMessageAsync(
                conversationId,
                It.IsAny<Dictionary<Guid, bool>>(),
                It.IsAny<SendMessageResponse>()), Times.Once);
        }

        #endregion

        #region ForwardMessageAsync Tests

        [Fact]
        public async Task ForwardMessageAsync_SourceMessageNotVisible_ThrowsBadRequestException()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var sourceMessageId = Guid.NewGuid();
            var sender = TestDataFactory.CreateAccount(accountId: senderId);

            _accountRepositoryMock.Setup(x => x.GetAccountById(senderId)).ReturnsAsync(sender);
            _messageRepositoryMock
                .Setup(x => x.GetVisibleMessageForAccountAsync(sourceMessageId, senderId))
                .ReturnsAsync((Message?)null);

            var request = new ForwardMessageRequest
            {
                SourceMessageId = sourceMessageId,
                ConversationIds = new List<Guid> { Guid.NewGuid() }
            };

            // Act
            var act = () => _messageService.ForwardMessageAsync(senderId, request);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("This message is no longer available.");
        }

        [Fact]
        public async Task ForwardMessageAsync_PostShareSourceUnavailable_ShouldReturnUnavailablePreview()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var sourceMessageId = Guid.NewGuid();
            var targetConversationId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();

            var sender = TestDataFactory.CreateAccount(accountId: senderId);
            var sourceMessage = TestDataFactory.CreateMessage(messageId: sourceMessageId, senderId: Guid.NewGuid(), conversationId: Guid.NewGuid());
            sourceMessage.MessageType = MessageTypeEnum.PostShare;
            sourceMessage.Content = "forward this";
            sourceMessage.SystemMessageDataJson = JsonSerializer.Serialize(new
            {
                postId,
                postCode = "POST-CODE",
                ownerId,
                ownerUsername = "owner_username",
                ownerDisplayName = "Owner Name",
                thumbnailUrl = "https://example.com/thumb.jpg",
                thumbnailMediaType = 1,
                contentSnippet = "snapshot content"
            });

            _accountRepositoryMock.Setup(x => x.GetAccountById(senderId)).ReturnsAsync(sender);
            _messageRepositoryMock
                .Setup(x => x.GetVisibleMessageForAccountAsync(sourceMessageId, senderId))
                .ReturnsAsync(sourceMessage);
            _postRepositoryMock
                .Setup(x => x.GetPostDetailByPostId(postId, senderId))
                .ReturnsAsync((PostDetailModel?)null);

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(targetConversationId))
                .ReturnsAsync(TestDataFactory.CreateConversation(targetConversationId));
            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(targetConversationId, senderId))
                .ReturnsAsync(true);

            _messageRepositoryMock
                .Setup(x => x.AddMessageAsync(It.IsAny<Message>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<SendMessageResponse>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<SendMessageResponse>> operation, Func<Task>? _) => operation());

            _mapperMock.Setup(x => x.Map<AccountChatInfoResponse>(sender)).Returns(new AccountChatInfoResponse
            {
                AccountId = senderId,
                Username = sender.Username,
                FullName = sender.FullName,
                AvatarUrl = sender.AvatarUrl,
                IsActive = true
            });

            _mapperMock
                .Setup(x => x.Map<SendMessageResponse>(It.IsAny<Message>()))
                .Returns<Message>(message => new SendMessageResponse
                {
                    MessageId = message.MessageId,
                    ConversationId = message.ConversationId,
                    MessageType = message.MessageType,
                    SentAt = message.SentAt
                });

            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(targetConversationId))
                .ReturnsAsync(new Dictionary<Guid, bool> { { senderId, false } });
            _realtimeServiceMock
                .Setup(x => x.NotifyNewMessageAsync(
                    targetConversationId,
                    It.IsAny<Dictionary<Guid, bool>>(),
                    It.IsAny<SendMessageResponse>()))
                .Returns(Task.CompletedTask);

            var request = new ForwardMessageRequest
            {
                SourceMessageId = sourceMessageId,
                ConversationIds = new List<Guid> { targetConversationId }
            };

            // Act
            var result = await _messageService.ForwardMessageAsync(senderId, request);

            // Assert
            result.TotalRequested.Should().Be(1);
            result.TotalSucceeded.Should().Be(1);
            result.TotalFailed.Should().Be(0);
            result.Results.Should().HaveCount(1);
            result.Results[0].IsSuccess.Should().BeTrue();
            result.Results[0].Message.Should().NotBeNull();
            result.Results[0].Message!.MessageType.Should().Be(MessageTypeEnum.PostShare);
            result.Results[0].Message!.PostShareInfo.Should().NotBeNull();
            result.Results[0].Message!.PostShareInfo!.IsPostUnavailable.Should().BeTrue();
            result.Results[0].Message!.PostShareInfo!.ThumbnailUrl.Should().BeNull();
            result.Results[0].Message!.PostShareInfo!.ContentSnippet.Should().BeNull();

            _postRepositoryMock.Verify(x => x.GetPostDetailByPostId(postId, senderId), Times.Once);
        }

        [Fact]
        public async Task ForwardMessageAsync_TextSourceWithMentions_ShouldForwardAsPlainText()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var sourceMessageId = Guid.NewGuid();
            var targetConversationId = Guid.NewGuid();
            var mentionedAccountId = Guid.NewGuid();

            var sender = TestDataFactory.CreateAccount(accountId: senderId);
            var sourceMessage = TestDataFactory.CreateMessage(
                messageId: sourceMessageId,
                senderId: Guid.NewGuid(),
                conversationId: Guid.NewGuid());
            sourceMessage.MessageType = MessageTypeEnum.Text;
            sourceMessage.Content = $"hello @[john.doe]({mentionedAccountId}) and @mike";

            Message? createdMessage = null;

            _accountRepositoryMock.Setup(x => x.GetAccountById(senderId)).ReturnsAsync(sender);
            _messageRepositoryMock
                .Setup(x => x.GetVisibleMessageForAccountAsync(sourceMessageId, senderId))
                .ReturnsAsync(sourceMessage);

            _conversationRepositoryMock
                .Setup(x => x.GetConversationByIdAsync(targetConversationId))
                .ReturnsAsync(TestDataFactory.CreateConversation(targetConversationId));
            _conversationMemberRepositoryMock
                .Setup(x => x.IsMemberOfConversation(targetConversationId, senderId))
                .ReturnsAsync(true);

            _messageRepositoryMock
                .Setup(x => x.AddMessageAsync(It.IsAny<Message>()))
                .Callback<Message>(message => createdMessage = message)
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<SendMessageResponse>>>(),
                    It.IsAny<Func<Task>?>()))
                .Returns((Func<Task<SendMessageResponse>> operation, Func<Task>? _) => operation());

            _mapperMock.Setup(x => x.Map<AccountChatInfoResponse>(sender)).Returns(new AccountChatInfoResponse
            {
                AccountId = senderId,
                Username = sender.Username,
                FullName = sender.FullName,
                AvatarUrl = sender.AvatarUrl,
                IsActive = true
            });

            _mapperMock
                .Setup(x => x.Map<SendMessageResponse>(It.IsAny<Message>()))
                .Returns<Message>(message => new SendMessageResponse
                {
                    MessageId = message.MessageId,
                    ConversationId = message.ConversationId,
                    Content = message.Content,
                    MessageType = message.MessageType,
                    SentAt = message.SentAt
                });

            _conversationMemberRepositoryMock
                .Setup(x => x.GetMembersWithMuteStatusAsync(targetConversationId))
                .ReturnsAsync(new Dictionary<Guid, bool> { { senderId, false } });
            _realtimeServiceMock
                .Setup(x => x.NotifyNewMessageAsync(
                    targetConversationId,
                    It.IsAny<Dictionary<Guid, bool>>(),
                    It.IsAny<SendMessageResponse>()))
                .Returns(Task.CompletedTask);

            var request = new ForwardMessageRequest
            {
                SourceMessageId = sourceMessageId,
                ConversationIds = new List<Guid> { targetConversationId }
            };

            // Act
            var result = await _messageService.ForwardMessageAsync(senderId, request);

            // Assert
            result.TotalRequested.Should().Be(1);
            result.TotalSucceeded.Should().Be(1);
            result.TotalFailed.Should().Be(0);

            createdMessage.Should().NotBeNull();
            createdMessage!.Content.Should().Be("hello @john.doe and @mike");
        }

        #endregion

        #region RecallMessageAsync Tests

        [Fact]
        public async Task RecallMessageAsync_MessageNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            _messageRepositoryMock.Setup(x => x.GetMessageByIdAsync(messageId))
                .ReturnsAsync((Message?)null);

            // Act
            var act = () => _messageService.RecallMessageAsync(messageId, accountId);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("Message not found.");
        }

        [Fact]
        public async Task RecallMessageAsync_NotOwner_ThrowsForbiddenException()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var otherAccountId = Guid.NewGuid();
            var message = TestDataFactory.CreateMessage(messageId: messageId, senderId: ownerId);

            _messageRepositoryMock.Setup(x => x.GetMessageByIdAsync(messageId))
                .ReturnsAsync(message);

            // Act
            var act = () => _messageService.RecallMessageAsync(messageId, otherAccountId);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You can only recall your own messages.");
        }

        [Fact]
        public async Task RecallMessageAsync_AlreadyRecalled_ReturnsWithoutCommitOrRealtime()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var recalledAt = DateTime.UtcNow.AddMinutes(-1);
            var message = TestDataFactory.CreateMessage(messageId: messageId, senderId: accountId);
            message.IsRecalled = true;
            message.RecalledAt = recalledAt;

            _messageRepositoryMock.Setup(x => x.GetMessageByIdAsync(messageId))
                .ReturnsAsync(message);

            // Act
            var result = await _messageService.RecallMessageAsync(messageId, accountId);

            // Assert
            result.MessageId.Should().Be(messageId);
            result.ConversationId.Should().Be(message.ConversationId);
            result.RecalledAt.Should().Be(recalledAt);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Never);
            _realtimeServiceMock.Verify(x => x.NotifyMessageRecalledAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task RecallMessageAsync_ValidRequest_UpdatesMessageAndNotifiesMembers()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var message = TestDataFactory.CreateMessage(messageId: messageId, senderId: accountId, conversationId: conversationId);

            _messageRepositoryMock.Setup(x => x.GetMessageByIdAsync(messageId))
                .ReturnsAsync(message);

            // Act
            var result = await _messageService.RecallMessageAsync(messageId, accountId);

            // Assert
            result.MessageId.Should().Be(messageId);
            result.ConversationId.Should().Be(conversationId);
            result.RecalledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            message.IsRecalled.Should().BeTrue();
            message.RecalledAt.Should().NotBeNull();

            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
            _realtimeServiceMock.Verify(x => x.NotifyMessageRecalledAsync(
                conversationId,
                messageId,
                accountId,
                It.IsAny<DateTime>()), Times.Once);
        }

        #endregion
    }
}
