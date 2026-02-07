using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;

namespace SocialNetwork.Application.Services.RealtimeServices
{
    /// <summary>
    /// Centralized service for all SignalR realtime notifications.
    /// This abstracts SignalR logic from controllers and services.
    /// </summary>
    public interface IRealtimeService
    {
        // ==================== POST NOTIFICATIONS ====================
        
        /// <summary>
        /// Notify when a new post is created (for profile post count update)
        /// </summary>
        Task NotifyPostCreatedAsync(Guid accountId, PostDetailResponse post);

        /// <summary>
        /// Notify when a post is updated (content + privacy)
        /// </summary>
        Task NotifyPostUpdatedAsync(Guid postId, Guid accountId, PostDetailResponse post);

        /// <summary>
        /// Notify when only post content is updated
        /// </summary>
        Task NotifyPostContentUpdatedAsync(Guid postId, Guid accountId, PostUpdateContentResponse content);

        /// <summary>
        /// Notify when a post is deleted
        /// </summary>
        Task NotifyPostDeletedAsync(Guid postId, Guid? accountId);

        /// <summary>
        /// Notify when post react count changes
        /// </summary>
        Task NotifyPostReactUpdatedAsync(Guid postId, int reactCount);

        // ==================== COMMENT NOTIFICATIONS ====================

        /// <summary>
        /// Notify when a new comment is created
        /// </summary>
        Task NotifyCommentCreatedAsync(Guid postId, CommentResponse comment, int? parentReplyCount);

        /// <summary>
        /// Notify when a comment is updated
        /// </summary>
        Task NotifyCommentUpdatedAsync(Guid postId, CommentResponse comment);

        /// <summary>
        /// Notify when a comment is deleted
        /// </summary>
        Task NotifyCommentDeletedAsync(Guid postId, Guid commentId, Guid? parentCommentId, int? totalPostComments, int? parentReplyCount);

        /// <summary>
        /// Notify when comment react count changes
        /// </summary>
        Task NotifyCommentReactUpdatedAsync(Guid postId, Guid commentId, int reactCount);

        // ==================== FOLLOW NOTIFICATIONS ====================

        /// <summary>
        /// Notify when follow status changes (follow/unfollow)
        /// </summary>
        Task NotifyFollowChangedAsync(Guid currentId, Guid targetId, string action, int targetFollowers, int targetFollowing, int myFollowers, int myFollowing);

        // ==================== MESSAGE NOTIFICATIONS ====================

        /// <summary>
        /// Notify when a new message is sent
        /// </summary>
        Task NotifyNewMessageAsync(Guid conversationId, SendMessageResponse message);
    }
}
