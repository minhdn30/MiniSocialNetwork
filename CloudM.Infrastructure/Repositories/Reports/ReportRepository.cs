using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Helpers;

namespace CloudM.Infrastructure.Repositories.Reports
{
    public class ReportRepository : IReportRepository
    {
        private readonly AppDbContext _context;

        public ReportRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> CanSubmitReportAsync(Guid currentId, ModerationTargetTypeEnum targetType, Guid targetId)
        {
            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            var followedIds = _context.Follows
                .AsNoTracking()
                .Where(follow => follow.FollowerId == currentId)
                .Select(follow => follow.FollowedId);

            return targetType switch
            {
                ModerationTargetTypeEnum.Account => await _context.Accounts.AnyAsync(account =>
                    account.AccountId == targetId &&
                    account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(account.AccountId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(account.RoleId)),
                ModerationTargetTypeEnum.Post => await _context.Posts.AnyAsync(post =>
                    post.PostId == targetId &&
                    !post.IsDeleted &&
                    post.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(post.AccountId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(post.Account.RoleId) &&
                    (
                        post.AccountId == currentId ||
                        post.Privacy == PostPrivacyEnum.Public ||
                        (post.Privacy == PostPrivacyEnum.FollowOnly && followedIds.Contains(post.AccountId))
                    )),
                ModerationTargetTypeEnum.Story => await _context.Stories.AnyAsync(story =>
                    story.StoryId == targetId &&
                    !story.IsDeleted &&
                    story.ExpiresAt > DateTime.UtcNow &&
                    story.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(story.AccountId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(story.Account.RoleId) &&
                    (
                        story.AccountId == currentId ||
                        story.Privacy == StoryPrivacyEnum.Public ||
                        (story.Privacy == StoryPrivacyEnum.FollowOnly && followedIds.Contains(story.AccountId))
                    )),
                ModerationTargetTypeEnum.Comment => await _context.Comments.AnyAsync(comment =>
                    comment.CommentId == targetId &&
                    comment.ParentCommentId == null &&
                    comment.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(comment.AccountId) &&
                    !comment.Post.IsDeleted &&
                    comment.Post.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(comment.Post.AccountId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(comment.Account.RoleId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(comment.Post.Account.RoleId) &&
                    (
                        comment.Post.AccountId == currentId ||
                        comment.Post.Privacy == PostPrivacyEnum.Public ||
                        (comment.Post.Privacy == PostPrivacyEnum.FollowOnly && followedIds.Contains(comment.Post.AccountId))
                    )),
                ModerationTargetTypeEnum.Reply => await _context.Comments.AnyAsync(comment =>
                    comment.CommentId == targetId &&
                    comment.ParentCommentId != null &&
                    comment.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(comment.AccountId) &&
                    !comment.Post.IsDeleted &&
                    comment.Post.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(comment.Post.AccountId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(comment.Account.RoleId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(comment.Post.Account.RoleId) &&
                    (
                        comment.Post.AccountId == currentId ||
                        comment.Post.Privacy == PostPrivacyEnum.Public ||
                        (comment.Post.Privacy == PostPrivacyEnum.FollowOnly && followedIds.Contains(comment.Post.AccountId))
                    )),
                _ => false,
            };
        }

        public async Task<bool> HasPendingDuplicateAsync(Guid currentId, ModerationTargetTypeEnum targetType, Guid targetId)
        {
            return await _context.ModerationReports
                .AsNoTracking()
                .AnyAsync(report =>
                    report.ReporterAccountId == currentId &&
                    report.SourceType == ModerationReportSourceEnum.UserSubmitted &&
                    report.TargetType == targetType &&
                    report.TargetId == targetId &&
                    (
                        report.Status == ModerationReportStatusEnum.Open ||
                        report.Status == ModerationReportStatusEnum.InReview
                    ));
        }

        public async Task AddAsync(ModerationReport report)
        {
            await _context.ModerationReports.AddAsync(report);
        }
    }
}
