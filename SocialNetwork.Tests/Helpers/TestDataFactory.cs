using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;

namespace SocialNetwork.Tests.Helpers
{
    /// <summary>
    /// Factory class to create test data entities and DTOs.
    /// Follows Testing Patterns: centralized test data creation for consistency.
    /// </summary>
    public static class TestDataFactory
    {
        private static readonly Random _random = new();

        #region Entities

        public static Account CreateAccount(
            Guid? accountId = null,
            string? username = null,
            string? email = null,
            AccountStatusEnum status = AccountStatusEnum.Active)
        {
            return new Account
            {
                AccountId = accountId ?? Guid.NewGuid(),
                Username = username ?? $"user_{_random.Next(1000, 9999)}",
                Email = email ?? $"user{_random.Next(1000, 9999)}@test.com",
                PasswordHash = "$2a$11$hashedpassword",
                FullName = "Test User",
                Status = status,
                RoleId = 2,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Post CreatePost(
            Guid? postId = null,
            Guid? ownerId = null,
            string? content = null,
            PostPrivacyEnum privacy = PostPrivacyEnum.Public,
            bool isDeleted = false)
        {
            var ownerIdValue = ownerId ?? Guid.NewGuid();
            return new Post
            {
                PostId = postId ?? Guid.NewGuid(),
                AccountId = ownerIdValue,
                Content = content ?? "Test post content",
                Privacy = privacy,
                IsDeleted = isDeleted,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Comment CreateComment(
            Guid? commentId = null,
            Guid? postId = null,
            Guid? accountId = null,
            Guid? parentCommentId = null,
            string? content = null)
        {
            return new Comment
            {
                CommentId = commentId ?? Guid.NewGuid(),
                PostId = postId ?? Guid.NewGuid(),
                AccountId = accountId ?? Guid.NewGuid(),
                ParentCommentId = parentCommentId,
                Content = content ?? "Test comment content",
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Follow CreateFollow(
            Guid? followerId = null,
            Guid? followedId = null)
        {
            return new Follow
            {
                FollowerId = followerId ?? Guid.NewGuid(),
                FollowedId = followedId ?? Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Conversation CreateConversation(
            Guid? conversationId = null,
            bool isGroup = false)
        {
            return new Conversation
            {
                ConversationId = conversationId ?? Guid.NewGuid(),
                IsGroup = isGroup,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Message CreateMessage(
            Guid? messageId = null,
            Guid? conversationId = null,
            Guid? senderId = null,
            string? content = null)
        {
            return new Message
            {
                MessageId = messageId ?? Guid.NewGuid(),
                ConversationId = conversationId ?? Guid.NewGuid(),
                AccountId = senderId ?? Guid.NewGuid(),
                Content = content ?? "Test message",
                SentAt = DateTime.UtcNow
            };
        }

        public static PostReact CreatePostReact(
            Guid? postId = null,
            Guid? accountId = null)
        {
            return new PostReact
            {
                PostId = postId ?? Guid.NewGuid(),
                AccountId = accountId ?? Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
        }

        public static CommentReact CreateCommentReact(
            Guid? commentId = null,
            Guid? accountId = null)
        {
            return new CommentReact
            {
                CommentId = commentId ?? Guid.NewGuid(),
                AccountId = accountId ?? Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
        }

        #endregion

        #region Models

        public static AccountOnFeedModel CreateAccountOnFeedModel(
            Guid? accountId = null,
            string? fullName = null)
        {
            return new AccountOnFeedModel
            {
                AccountId = accountId ?? Guid.NewGuid(),
                FullName = fullName ?? "Test User",
                Username = $"user_{_random.Next(1000, 9999)}",
                AvatarUrl = null,
                Status = AccountStatusEnum.Active,
                IsFollowedByCurrentUser = false
            };
        }

        public static AccountBasicInfoModel CreateAccountBasicInfoModel(
            Guid? accountId = null,
            string? fullName = null)
        {
            return new AccountBasicInfoModel
            {
                AccountId = accountId ?? Guid.NewGuid(),
                FullName = fullName ?? "Test User",
                Username = $"user_{_random.Next(1000, 9999)}",
                AvatarUrl = null
            };
        }

        public static PostFeedModel CreatePostFeedModel(
            Guid? postId = null,
            Guid? ownerId = null)
        {
            return new PostFeedModel
            {
                PostId = postId ?? Guid.NewGuid(),
                PostCode = "POST123",
                Author = CreateAccountOnFeedModel(ownerId),
                Content = "Test post content",
                Privacy = PostPrivacyEnum.Public,
                CreatedAt = DateTime.UtcNow,
                ReactCount = 0,
                CommentCount = 0,
                IsReactedByCurrentUser = false,
                IsOwner = false
            };
        }

        public static CommentWithReplyCountModel CreateCommentWithReplyCountModel(
            Guid? commentId = null,
            Guid? ownerId = null)
        {
            return new CommentWithReplyCountModel
            {
                CommentId = commentId ?? Guid.NewGuid(),
                PostId = Guid.NewGuid(),
                Owner = CreateAccountBasicInfoModel(ownerId),
                Content = "Test comment",
                CreatedAt = DateTime.UtcNow,
                ReactCount = 0,
                ReplyCount = 0,
                IsCommentReactedByCurrentUser = false,
                PostOwnerId = Guid.NewGuid()
            };
        }

        public static ProfileInfoModel CreateProfileInfoModel(
            Guid? accountId = null)
        {
            return new ProfileInfoModel
            {
                AccountId = accountId ?? Guid.NewGuid(),
                FollowerPrivacy = AccountPrivacyEnum.Public,
                FollowingPrivacy = AccountPrivacyEnum.Public,
                FollowerCount = 10,
                FollowingCount = 5,
                PostCount = 20
            };
        }

        public static MessageBasicModel CreateMessageBasicModel(
            Guid? messageId = null,
            Guid? senderId = null)
        {
            return new MessageBasicModel
            {
                MessageId = messageId ?? Guid.NewGuid(),
                Sender = CreateAccountBasicInfoModel(senderId),
                Content = "Test message",
                SentAt = DateTime.UtcNow
            };
        }

        #endregion

        #region DTOs - Requests

        public static AccountCreateRequest CreateAccountCreateRequest(
            string? username = null,
            string? email = null)
        {
            return new AccountCreateRequest
            {
                Username = username ?? $"user_{_random.Next(1000, 9999)}",
                Email = email ?? $"user{_random.Next(1000, 9999)}@test.com",
                Password = "Password123!",
                FullName = "Test User"
            };
        }

        public static RegisterDTO CreateRegisterDTO(
            string? username = null,
            string? email = null)
        {
            return new RegisterDTO
            {
                Username = username ?? $"user_{_random.Next(1000, 9999)}",
                Email = email ?? $"user{_random.Next(1000, 9999)}@test.com",
                Password = "Password123!",
                FullName = "Test User"
            };
        }

        public static LoginRequest CreateLoginRequest(
            string? email = null,
            string? password = null)
        {
            return new LoginRequest
            {
                Email = email ?? "test@example.com",
                Password = password ?? "Password123!"
            };
        }

        public static PostCreateRequest CreatePostCreateRequest(
            string? content = null,
            PostPrivacyEnum privacy = PostPrivacyEnum.Public)
        {
            return new PostCreateRequest
            {
                Content = content ?? "Test post content",
                Privacy = (int)privacy
            };
        }

        public static CommentCreateRequest CreateCommentCreateRequest(
            string? content = null)
        {
            return new CommentCreateRequest
            {
                Content = content ?? "Test comment"
            };
        }

        #endregion

        #region DTOs - Responses

        public static AccountDetailResponse CreateAccountDetailResponse(
            Guid? accountId = null)
        {
            return new AccountDetailResponse
            {
                AccountId = accountId ?? Guid.NewGuid(),
                Username = "testuser",
                Email = "test@example.com",
                FullName = "Test User"
            };
        }


        public static CommentResponse CreateCommentResponse(
            Guid? commentId = null)
        {
            return new CommentResponse
            {
                CommentId = commentId ?? Guid.NewGuid(),
                PostId = Guid.NewGuid(),
                Owner = new AccountBasicInfoResponse
                {
                    AccountId = Guid.NewGuid(),
                    FullName = "Test User",
                    Username = "testuser"
                },
                Content = "Test comment",
                CreatedAt = DateTime.UtcNow
            };
        }

        #endregion

        #region Helpers

        public static List<T> CreateList<T>(Func<T> factory, int count = 5)
        {
            var list = new List<T>();
            for (int i = 0; i < count; i++)
            {
                list.Add(factory());
            }
            return list;
        }

        #endregion
    }
}
