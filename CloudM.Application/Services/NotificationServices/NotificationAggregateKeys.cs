namespace CloudM.Application.Services.NotificationServices
{
    public static class NotificationAggregateKeys
    {
        public static string Follow(Guid actorId) => $"follow:{actorId}";
        public static string FollowAutoAcceptSummary(Guid targetId) => $"follow-auto-accept:{targetId}";
        public static string FollowRequest(Guid actorId) => $"follow-request:{actorId}";
        public static string FollowRequestAccepted(Guid actorId) => $"follow-request-accepted:{actorId}";
        public static string PostComment(Guid postId) => $"post-comment:{postId}";
        public static string CommentReply(Guid parentCommentId) => $"comment-reply:{parentCommentId}";
        public static string PostTag(Guid postId) => $"post-tag:{postId}";
        public static string CommentMention(Guid commentId) => $"comment-mention:{commentId}";
        public static string StoryReply(Guid storyId) => $"story-reply:{storyId}";
        public static string PostReact(Guid postId) => $"post-react:{postId}";
        public static string StoryReact(Guid storyId) => $"story-react:{storyId}";
        public static string CommentReact(Guid commentId) => $"comment-react:{commentId}";
        public static string ReplyReact(Guid commentId) => $"reply-react:{commentId}";
    }
}
