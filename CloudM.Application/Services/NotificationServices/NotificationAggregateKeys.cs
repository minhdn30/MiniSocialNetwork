namespace CloudM.Application.Services.NotificationServices
{
    public static class NotificationAggregateKeys
    {
        public static string Follow(Guid actorId) => $"follow:{actorId}";
        public static string PostComment(Guid postId) => $"post-comment:{postId}";
        public static string CommentReply(Guid parentCommentId) => $"comment-reply:{parentCommentId}";
        public static string PostTag(Guid postId) => $"post-tag:{postId}";
        public static string CommentMention(Guid commentId) => $"comment-mention:{commentId}";
        public static string StoryReply(Guid storyId) => $"story-reply:{storyId}";
        public static string PostReact(Guid postId) => $"post-react:{postId}";
        public static string StoryReact(Guid storyId) => $"story-react:{storyId}";
    }
}
