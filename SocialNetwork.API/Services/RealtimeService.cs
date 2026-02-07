using Microsoft.AspNetCore.SignalR;
using SocialNetwork.API.Hubs;
using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.Services.RealtimeServices;

namespace SocialNetwork.API.Services
{
    /// <summary>
    /// Implementation of IRealtimeService using SignalR hubs.
    /// Centralizes all realtime notification logic.
    /// </summary>
    public class RealtimeService : IRealtimeService
    {
        private readonly IHubContext<PostHub> _postHubContext;
        private readonly IHubContext<UserHub> _userHubContext;
        private readonly IHubContext<ChatHub> _chatHubContext;

        public RealtimeService(
            IHubContext<PostHub> postHubContext,
            IHubContext<UserHub> userHubContext,
            IHubContext<ChatHub> chatHubContext)
        {
            _postHubContext = postHubContext;
            _userHubContext = userHubContext;
            _chatHubContext = chatHubContext;
        }

        // ==================== POST NOTIFICATIONS ====================

        public async Task NotifyPostCreatedAsync(Guid accountId, PostDetailResponse post)
        {
            await _userHubContext.Clients.Group($"Account-{accountId}")
                .SendAsync("ReceiveNewPost", new
                {
                    postId = post.PostId,
                    accountId = accountId
                });
        }

        public async Task NotifyPostUpdatedAsync(Guid postId, Guid accountId, PostDetailResponse post)
        {
            // Notify post detail viewers
            await _postHubContext.Clients.Group($"Post-{postId}")
                .SendAsync("ReceiveUpdatedPost", post);

            // Notify post list viewers (feed/profile)
            await _postHubContext.Clients.Group($"PostList-{accountId}")
                .SendAsync("ReceiveUpdatedPost", post);
        }

        public async Task NotifyPostContentUpdatedAsync(Guid postId, Guid accountId, PostUpdateContentResponse content)
        {
            await _postHubContext.Clients.Group($"Post-{postId}")
                .SendAsync("ReceiveUpdatedPostContent", content);

            await _postHubContext.Clients.Group($"PostList-{accountId}")
                .SendAsync("ReceiveUpdatedPostContent", content);
        }

        public async Task NotifyPostDeletedAsync(Guid postId, Guid? accountId)
        {
            await _postHubContext.Clients.Group($"Post-{postId}")
                .SendAsync("ReceiveDeletedPost", postId, accountId);

            if (accountId.HasValue)
            {
                await _userHubContext.Clients.Group($"Account-{accountId}")
                    .SendAsync("ReceiveDeletedPost", postId, accountId);
            }
        }

        public async Task NotifyPostReactUpdatedAsync(Guid postId, int reactCount)
        {
            await _postHubContext.Clients.Group($"Post-{postId}")
                .SendAsync("ReceiveReactUpdate", postId, reactCount);
        }

        // ==================== COMMENT NOTIFICATIONS ====================

        public async Task NotifyCommentCreatedAsync(Guid postId, CommentResponse comment, int? parentReplyCount)
        {
            await _postHubContext.Clients.Group($"Post-{postId}")
                .SendAsync("ReceiveNewComment", comment, parentReplyCount);
        }

        public async Task NotifyCommentUpdatedAsync(Guid postId, CommentResponse comment)
        {
            await _postHubContext.Clients.Group($"Post-{postId}")
                .SendAsync("ReceiveUpdatedComment", comment);
        }

        public async Task NotifyCommentDeletedAsync(Guid postId, Guid commentId, Guid? parentCommentId, int? totalPostComments, int? parentReplyCount)
        {
            await _postHubContext.Clients.Group($"Post-{postId}")
                .SendAsync("ReceiveDeletedComment", commentId, parentCommentId, totalPostComments, parentReplyCount, postId);
        }

        public async Task NotifyCommentReactUpdatedAsync(Guid postId, Guid commentId, int reactCount)
        {
            await _postHubContext.Clients.Group($"Post-{postId}")
                .SendAsync("ReceiveCommentReactUpdate", commentId, reactCount);
        }

        // ==================== FOLLOW NOTIFICATIONS ====================

        public async Task NotifyFollowChangedAsync(Guid currentId, Guid targetId, string action, int targetFollowers, int targetFollowing, int myFollowers, int myFollowing)
        {
            // Notify target user (their follower count changed)
            await _userHubContext.Clients.Group($"Account-{targetId}")
                .SendAsync("ReceiveFollowNotification", new
                {
                    CurrentId = currentId,
                    TargetId = targetId,
                    Action = action,
                    Followers = targetFollowers,
                    Following = targetFollowing
                });

            // Notify current user (their following count changed)
            var myAction = action == "follow" ? "follow_sent" : "unfollow_sent";
            await _userHubContext.Clients.Group($"Account-{currentId}")
                .SendAsync("ReceiveFollowNotification", new
                {
                    CurrentId = currentId,
                    TargetId = currentId,
                    Action = myAction,
                    Followers = myFollowers,
                    Following = myFollowing
                });
        }

        // ==================== MESSAGE NOTIFICATIONS ====================

        public async Task NotifyNewMessageAsync(Guid conversationId, SendMessageResponse message)
        {
            await _chatHubContext.Clients.Group(conversationId.ToString())
                .SendAsync("ReceiveNewMessage", message);
        }
    }
}
