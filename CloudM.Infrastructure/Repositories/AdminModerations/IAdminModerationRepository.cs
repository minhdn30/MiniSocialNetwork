using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AdminModerations
{
    public interface IAdminModerationRepository
    {
        Task<AdminModerationItemModel?> LookupAsync(ModerationTargetTypeEnum targetType, string keyword);
        Task<Post?> GetTrackedPostAsync(Guid postId);
        Task<Story?> GetTrackedStoryAsync(Guid storyId);
        Task<Comment?> GetTrackedCommentAsync(Guid commentId);
        Task DeleteCommentThreadAsync(Guid commentId);
        Task<bool> TargetExistsAsync(ModerationTargetTypeEnum targetType, Guid targetId);
    }
}
