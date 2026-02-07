using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;

namespace SocialNetwork.Application.Services.RealtimeServices
{
   
    /// Centralized service for all SignalR realtime notifications.
    /// This abstracts SignalR logic from controllers and services.
    
    public interface IRealtimeService
    {
        // ==================== POST NOTIFICATIONS ====================
        
       
        /// Notify when a new post is created (for profile post count update)
        
        Task NotifyPostCreatedAsync(Guid accountId, PostDetailResponse post);

       
        /// Notify when a post is updated (content + privacy)
        
        Task NotifyPostUpdatedAsync(Guid postId, Guid accountId, PostDetailResponse post);

       
        /// Notify when only post content is updated
        
        Task NotifyPostContentUpdatedAsync(Guid postId, Guid accountId, PostUpdateContentResponse content);

       
        /// Notify when a post is deleted
        
        Task NotifyPostDeletedAsync(Guid postId, Guid? accountId);

       
        /// Notify when post react count changes
        
        Task NotifyPostReactUpdatedAsync(Guid postId, int reactCount);

        // ==================== COMMENT NOTIFICATIONS ====================

        /// Notify when a new comment is created
        Task NotifyCommentCreatedAsync(Guid postId, CommentResponse comment, int? parentReplyCount);

        /// Notify when a comment is updated
        Task NotifyCommentUpdatedAsync(Guid postId, CommentResponse comment);

       
        /// Notify when a comment is deleted
        
        Task NotifyCommentDeletedAsync(Guid postId, Guid commentId, Guid? parentCommentId, int? totalPostComments, int? parentReplyCount);

       
        /// Notify when comment react count changes
        
        Task NotifyCommentReactUpdatedAsync(Guid postId, Guid commentId, int reactCount);

        // ==================== FOLLOW NOTIFICATIONS ====================

       
        /// Notify when follow status changes (follow/unfollow)
        
        Task NotifyFollowChangedAsync(Guid currentId, Guid targetId, string action, int targetFollowers, int targetFollowing, int myFollowers, int myFollowing);

        // ==================== MESSAGE NOTIFICATIONS ====================

       
        /// Notify when a new message is sent
        
        Task NotifyNewMessageAsync(Guid conversationId, SendMessageResponse message);
    }
}
