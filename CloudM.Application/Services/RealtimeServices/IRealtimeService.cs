using CloudM.Application.DTOs.CommentDTOs;
using CloudM.Application.DTOs.MessageDTOs;
using CloudM.Application.DTOs.PostDTOs;
using System.Collections.Generic;

namespace CloudM.Application.Services.RealtimeServices
{
   
    // centralized service for all signalr realtime notifications
    // this abstracts signalr logic from controllers and services
    
    public interface IRealtimeService
    {
        // post notifications
        
        // notify when a new post is created (for profile post count update)
        Task NotifyPostCreatedAsync(Guid accountId, PostDetailResponse post);

        // notify when a post is updated (content + privacy)
        Task NotifyPostUpdatedAsync(Guid postId, Guid accountId, PostDetailResponse post);

        // notify when only post content is updated
        Task NotifyPostContentUpdatedAsync(Guid postId, Guid accountId, PostUpdateContentResponse content);

        // notify when a post is deleted
        Task NotifyPostDeletedAsync(Guid postId, Guid? accountId);

        // notify when post react count changes
        Task NotifyPostReactUpdatedAsync(Guid postId, int reactCount);

        // comment notifications

        // notify when a new comment is created
        Task NotifyCommentCreatedAsync(Guid postId, CommentResponse comment, int? parentReplyCount);


        // notify when a comment is updated
        Task NotifyCommentUpdatedAsync(Guid postId, CommentResponse comment);

        // notify when a comment is deleted
        Task NotifyCommentDeletedAsync(Guid postId, Guid commentId, Guid? parentCommentId, int? totalPostComments, int? parentReplyCount);

        // notify when comment react count changes
        Task NotifyCommentReactUpdatedAsync(Guid postId, Guid commentId, int reactCount);

        // follow notifications

        // notify when follow status changes (follow/unfollow)
        Task NotifyFollowChangedAsync(Guid currentId, Guid targetId, string action, int targetFollowers, int targetFollowing, int myFollowers, int myFollowing);

        // account notifications

        // notify when account profile information is updated
        Task NotifyProfileUpdatedAsync(Guid accountId, CloudM.Application.DTOs.AccountDTOs.AccountDetailResponse account);

        // notify when account settings are updated
        Task NotifyAccountSettingsUpdatedAsync(Guid accountId, CloudM.Application.DTOs.AccountSettingDTOs.AccountSettingsResponse settings);

        // message notifications

        // notify when a message is hidden (recalled for user only)
        Task NotifyMessageHiddenAsync(Guid accountId, Guid conversationId, Guid messageId);

        // notify active clients in the conversation room when a message is recalled
        Task NotifyMessageRecalledAsync(Guid conversationId, Guid messageId, Guid recalledByAccountId, DateTime recalledAt);

        // notify active clients in the conversation room when message reactions are updated
        Task NotifyMessageReactUpdatedAsync(Guid conversationId, Guid messageId, Guid updatedByAccountId);

        // notify when a new message is sent (includes per-member mute status)
        Task NotifyNewMessageAsync(Guid conversationId, Dictionary<Guid, bool> memberMuteMap, SendMessageResponse message);

        // notify a specific user that their mute state changed for a conversation
        Task NotifyConversationMuteUpdatedAsync(Guid accountId, Guid conversationId, bool isMuted);

        // notify conversation participants that a nickname was changed
        Task NotifyConversationNicknameUpdatedAsync(Guid conversationId, Guid targetAccountId, string? nickname, Guid updatedBy, IEnumerable<Guid> memberIds);

        // notify active clients in the conversation room when the conversation theme was changed
        Task NotifyConversationThemeUpdatedAsync(Guid conversationId, string? theme, Guid updatedBy);

        // notify active clients in the conversation room when group name/avatar/owner was changed
        Task NotifyGroupConversationInfoUpdatedAsync(Guid conversationId, string? conversationName, string? conversationAvatar, Guid? ownerAccountId, Guid updatedBy);

        // notify a specific user that a conversation should be removed from their chat list
        Task NotifyConversationRemovedAsync(Guid accountId, Guid conversationId, string reason);
    }
}
