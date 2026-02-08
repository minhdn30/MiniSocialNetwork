using Microsoft.AspNetCore.SignalR;
using SocialNetwork.API.Hubs;
using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.Services.RealtimeServices;

namespace SocialNetwork.API.Services
{
    // implementation of irealtimeservice using signalr hubs
    // centralizes all realtime notification logic
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

        // post notifications

        public async Task NotifyPostCreatedAsync(Guid accountId, PostDetailResponse post)
        {
            // send full post object so ui can prepend it immediately
            await _userHubContext.Clients.Group($"Account-{accountId}")
                .SendAsync("ReceiveNewPost", post);
        }

        public async Task NotifyPostUpdatedAsync(Guid postId, Guid accountId, PostDetailResponse post)
        {
            // notify post detail viewers
            await _postHubContext.Clients.Group($"Post-{postId}")
                .SendAsync("ReceiveUpdatedPost", post);

            // notify post list viewers (feed/profile)
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

        // comment notifications

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

        // follow notifications

        public async Task NotifyFollowChangedAsync(Guid currentId, Guid targetId, string action, int targetFollowers, int targetFollowing, int myFollowers, int myFollowing)
        {
            // notify target user (their follower count changed)
            await _userHubContext.Clients.Group($"Account-{targetId}")
                .SendAsync("ReceiveFollowNotification", new
                {
                    CurrentId = currentId,
                    TargetId = targetId,
                    Action = action,
                    Followers = targetFollowers,
                    Following = targetFollowing
                });

            // notify current user (their following count changed)
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
        
        // account notifications

        public async Task NotifyProfileUpdatedAsync(Guid accountId, SocialNetwork.Application.DTOs.AccountDTOs.AccountDetailResponse account)
        {
            await _userHubContext.Clients.Group($"Account-{accountId}")
                .SendAsync("ReceiveProfileUpdate", account);
        }

        public async Task NotifyAccountSettingsUpdatedAsync(Guid accountId, SocialNetwork.Application.DTOs.AccountSettingDTOs.AccountSettingsResponse settings)
        {
            await _userHubContext.Clients.Group($"Account-{accountId}")
                .SendAsync("ReceiveAccountSettingsUpdate", settings);
        }

        // message notifications

        public async Task NotifyNewMessageAsync(Guid conversationId, SendMessageResponse message)
        {
            await _chatHubContext.Clients.Group(conversationId.ToString())
                .SendAsync("ReceiveNewMessage", message);
        }
    }
}
