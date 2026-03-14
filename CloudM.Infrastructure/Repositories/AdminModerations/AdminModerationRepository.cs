using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AdminModerations
{
    public class AdminModerationRepository : IAdminModerationRepository
    {
        private readonly AppDbContext _context;

        public AdminModerationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdminModerationItemModel?> LookupAsync(ModerationTargetTypeEnum targetType, string keyword)
        {
            var normalizedKeyword = (keyword ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                return null;
            }

            return targetType switch
            {
                ModerationTargetTypeEnum.Post => await LookupPostAsync(normalizedKeyword),
                ModerationTargetTypeEnum.Story => await LookupStoryAsync(normalizedKeyword),
                ModerationTargetTypeEnum.Comment => await LookupCommentAsync(normalizedKeyword, false),
                ModerationTargetTypeEnum.Reply => await LookupCommentAsync(normalizedKeyword, true),
                _ => null,
            };
        }

        public async Task<Post?> GetTrackedPostAsync(Guid postId)
        {
            return await _context.Posts
                .Include(post => post.Account)
                .FirstOrDefaultAsync(post => post.PostId == postId);
        }

        public async Task<Story?> GetTrackedStoryAsync(Guid storyId)
        {
            return await _context.Stories
                .Include(story => story.Account)
                .FirstOrDefaultAsync(story => story.StoryId == storyId);
        }

        public async Task<Comment?> GetTrackedCommentAsync(Guid commentId)
        {
            return await _context.Comments
                .Include(comment => comment.Account)
                .Include(comment => comment.Post)
                .FirstOrDefaultAsync(comment => comment.CommentId == commentId);
        }

        public async Task DeleteCommentThreadAsync(Guid commentId)
        {
            var commentsToDelete = await _context.Comments
                .Where(comment => comment.CommentId == commentId || comment.ParentCommentId == commentId)
                .ToListAsync();

            if (commentsToDelete.Count == 0)
            {
                return;
            }

            _context.Comments.RemoveRange(commentsToDelete);
        }

        public async Task<bool> TargetExistsAsync(ModerationTargetTypeEnum targetType, Guid targetId)
        {
            return targetType switch
            {
                ModerationTargetTypeEnum.Account => await _context.Accounts.AnyAsync(account => account.AccountId == targetId),
                ModerationTargetTypeEnum.Post => await _context.Posts.AnyAsync(post => post.PostId == targetId),
                ModerationTargetTypeEnum.Story => await _context.Stories.AnyAsync(story => story.StoryId == targetId),
                ModerationTargetTypeEnum.Comment => await _context.Comments.AnyAsync(comment => comment.CommentId == targetId && comment.ParentCommentId == null),
                ModerationTargetTypeEnum.Reply => await _context.Comments.AnyAsync(comment => comment.CommentId == targetId && comment.ParentCommentId != null),
                _ => false,
            };
        }

        private async Task<AdminModerationItemModel?> LookupPostAsync(string keyword)
        {
            var hasPostId = Guid.TryParse(keyword, out var postIdKeyword);
            var normalizedPostCode = (keyword ?? string.Empty).Trim();
            var loweredPostCode = normalizedPostCode.ToLowerInvariant();

            var matchedPost = await _context.Posts
                .AsNoTracking()
                .Where(post => (hasPostId && post.PostId == postIdKeyword) || post.PostCode == normalizedPostCode)
                .Select(post => new AdminModerationItemModel
                {
                    TargetId = post.PostId,
                    TargetType = ModerationTargetTypeEnum.Post,
                    OwnerAccountId = post.AccountId,
                    OwnerUsername = post.Account.Username,
                    OwnerFullname = post.Account.FullName,
                    OwnerEmail = post.Account.Email,
                    LookupLabel = post.PostCode,
                    PrimaryText = post.PostCode,
                    SecondaryText = post.IsDeleted ? "deleted" : "active",
                    ContentPreview = post.Content,
                    CurrentState = post.IsDeleted ? "removed" : "active",
                    IsRemoved = post.IsDeleted,
                    CanRestore = post.IsDeleted,
                    RelatedPostId = post.PostId,
                    RelatedPostCode = post.PostCode,
                    CreatedAt = post.CreatedAt,
                })
                .FirstOrDefaultAsync();

            if (matchedPost != null || string.Equals(normalizedPostCode, loweredPostCode, StringComparison.Ordinal))
            {
                return matchedPost;
            }

            return await _context.Posts
                .AsNoTracking()
                .Where(post => post.PostCode == loweredPostCode)
                .Select(post => new AdminModerationItemModel
                {
                    TargetId = post.PostId,
                    TargetType = ModerationTargetTypeEnum.Post,
                    OwnerAccountId = post.AccountId,
                    OwnerUsername = post.Account.Username,
                    OwnerFullname = post.Account.FullName,
                    OwnerEmail = post.Account.Email,
                    LookupLabel = post.PostCode,
                    PrimaryText = post.PostCode,
                    SecondaryText = post.IsDeleted ? "deleted" : "active",
                    ContentPreview = post.Content,
                    CurrentState = post.IsDeleted ? "removed" : "active",
                    IsRemoved = post.IsDeleted,
                    CanRestore = post.IsDeleted,
                    RelatedPostId = post.PostId,
                    RelatedPostCode = post.PostCode,
                    CreatedAt = post.CreatedAt,
                })
                .FirstOrDefaultAsync();
        }

        private async Task<AdminModerationItemModel?> LookupStoryAsync(string keyword)
        {
            if (!Guid.TryParse(keyword, out var storyIdKeyword))
            {
                return null;
            }

            return await _context.Stories
                .AsNoTracking()
                .Where(story => story.StoryId == storyIdKeyword)
                .Select(story => new AdminModerationItemModel
                {
                    TargetId = story.StoryId,
                    TargetType = ModerationTargetTypeEnum.Story,
                    OwnerAccountId = story.AccountId,
                    OwnerUsername = story.Account.Username,
                    OwnerFullname = story.Account.FullName,
                    OwnerEmail = story.Account.Email,
                    LookupLabel = story.StoryId.ToString(),
                    PrimaryText = story.ContentType.ToString(),
                    SecondaryText = story.IsDeleted ? "deleted" : "active",
                    ContentPreview = story.TextContent ?? story.MediaUrl,
                    CurrentState = story.IsDeleted ? "removed" : "active",
                    IsRemoved = story.IsDeleted,
                    CanRestore = story.IsDeleted,
                    CreatedAt = story.CreatedAt,
                })
                .FirstOrDefaultAsync();
        }

        private async Task<AdminModerationItemModel?> LookupCommentAsync(string keyword, bool requireReply)
        {
            if (!Guid.TryParse(keyword, out var commentIdKeyword))
            {
                return null;
            }

            return await _context.Comments
                .AsNoTracking()
                .Where(comment =>
                    comment.CommentId == commentIdKeyword &&
                    (requireReply ? comment.ParentCommentId != null : comment.ParentCommentId == null))
                .Select(comment => new AdminModerationItemModel
                {
                    TargetId = comment.CommentId,
                    TargetType = comment.ParentCommentId == null
                        ? ModerationTargetTypeEnum.Comment
                        : ModerationTargetTypeEnum.Reply,
                    OwnerAccountId = comment.AccountId,
                    OwnerUsername = comment.Account.Username,
                    OwnerFullname = comment.Account.FullName,
                    OwnerEmail = comment.Account.Email,
                    LookupLabel = comment.CommentId.ToString(),
                    PrimaryText = comment.ParentCommentId == null ? "comment" : "reply",
                    SecondaryText = "active",
                    ContentPreview = comment.Content,
                    CurrentState = "active",
                    IsRemoved = false,
                    CanRestore = false,
                    ParentCommentId = comment.ParentCommentId,
                    RelatedPostId = comment.PostId,
                    RelatedPostCode = comment.Post.PostCode,
                    CreatedAt = comment.CreatedAt,
                })
                .FirstOrDefaultAsync();
        }
    }
}
