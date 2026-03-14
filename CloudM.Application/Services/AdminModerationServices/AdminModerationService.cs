using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Application.Services.AdminAuditLogServices;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.AdminModerations;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.AdminModerationServices
{
    public class AdminModerationService : IAdminModerationService
    {
        private const int MaxReasonLength = 300;

        private readonly IAdminModerationRepository _adminModerationRepository;
        private readonly IAdminAuditLogService _adminAuditLogService;
        private readonly IUnitOfWork _unitOfWork;

        public AdminModerationService(
            IAdminModerationRepository adminModerationRepository,
            IAdminAuditLogService adminAuditLogService,
            IUnitOfWork unitOfWork)
        {
            _adminModerationRepository = adminModerationRepository;
            _adminAuditLogService = adminAuditLogService;
            _unitOfWork = unitOfWork;
        }

        public async Task<AdminModerationLookupResponse> LookupAsync(AdminModerationLookupRequest request)
        {
            var targetType = ParseTargetType(request.TargetType);
            var normalizedKeyword = NormalizeKeyword(request.Keyword);

            return new AdminModerationLookupResponse
            {
                TargetType = targetType.ToString(),
                Keyword = normalizedKeyword,
                Item = MapItem(await _adminModerationRepository.LookupAsync(targetType, normalizedKeyword)),
            };
        }

        public async Task<AdminModerationActionResponse> ApplyActionAsync(
            Guid adminId,
            Guid targetId,
            AdminModerationActionRequest request,
            string targetType,
            string? requesterIpAddress)
        {
            var normalizedReason = NormalizeReason(request.Reason);
            if (string.IsNullOrWhiteSpace(normalizedReason))
            {
                throw new BadRequestException("Reason is required.");
            }

            var parsedTargetType = ParseTargetType(targetType);
            var normalizedAction = NormalizeAction(request.Action);

            switch (parsedTargetType)
            {
                case ModerationTargetTypeEnum.Post:
                    await ApplyPostActionAsync(targetId, normalizedAction);
                    break;
                case ModerationTargetTypeEnum.Story:
                    await ApplyStoryActionAsync(targetId, normalizedAction);
                    break;
                case ModerationTargetTypeEnum.Comment:
                case ModerationTargetTypeEnum.Reply:
                    await ApplyCommentActionAsync(targetId, normalizedAction, parsedTargetType);
                    break;
                default:
                    throw new BadRequestException("This target type is not supported in moderation.");
            }

            await _adminAuditLogService.RecordAsync(new AdminAuditLogWriteRequest
            {
                AdminId = adminId,
                Module = "moderation",
                ActionType = BuildAuditActionType(parsedTargetType, normalizedAction),
                TargetType = parsedTargetType.ToString(),
                TargetId = targetId.ToString(),
                Summary = normalizedReason,
                RequestIp = requesterIpAddress,
            });
            await _unitOfWork.CommitAsync();

            var refreshedItem = await _adminModerationRepository.LookupAsync(parsedTargetType, targetId.ToString());
            if (refreshedItem == null)
            {
                refreshedItem = BuildRemovedFallback(parsedTargetType, targetId);
            }

            return new AdminModerationActionResponse
            {
                Action = normalizedAction,
                Reason = normalizedReason,
                Item = MapItem(refreshedItem),
            };
        }

        private async Task ApplyPostActionAsync(Guid postId, string action)
        {
            var post = await _adminModerationRepository.GetTrackedPostAsync(postId);
            if (post == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found.");
            }

            if (action == "hide")
            {
                if (post.IsDeleted)
                {
                    throw new BadRequestException("This post is already removed.");
                }

                post.IsDeleted = true;
                post.UpdatedAt = DateTime.UtcNow;
                return;
            }

            if (action == "restore")
            {
                if (!post.IsDeleted)
                {
                    throw new BadRequestException("This post is already active.");
                }

                post.IsDeleted = false;
                post.UpdatedAt = DateTime.UtcNow;
                return;
            }

            throw new BadRequestException("Unsupported moderation action for post.");
        }

        private async Task ApplyStoryActionAsync(Guid storyId, string action)
        {
            var story = await _adminModerationRepository.GetTrackedStoryAsync(storyId);
            if (story == null)
            {
                throw new NotFoundException($"Story with ID {storyId} not found.");
            }

            if (action == "hide")
            {
                if (story.IsDeleted)
                {
                    throw new BadRequestException("This story is already removed.");
                }

                story.IsDeleted = true;
                return;
            }

            if (action == "restore")
            {
                if (!story.IsDeleted)
                {
                    throw new BadRequestException("This story is already active.");
                }

                story.IsDeleted = false;
                return;
            }

            throw new BadRequestException("Unsupported moderation action for story.");
        }

        private async Task ApplyCommentActionAsync(Guid commentId, string action, ModerationTargetTypeEnum targetType)
        {
            if (action != "remove")
            {
                throw new BadRequestException(targetType == ModerationTargetTypeEnum.Comment
                    ? "Comments can only be removed."
                    : "Replies can only be removed.");
            }

            var comment = await _adminModerationRepository.GetTrackedCommentAsync(commentId);
            if (comment == null)
            {
                throw new NotFoundException($"Comment with ID {commentId} not found.");
            }

            var isReply = comment.ParentCommentId.HasValue;
            if (targetType == ModerationTargetTypeEnum.Comment && isReply)
            {
                throw new BadRequestException("This target is a reply, not a root comment.");
            }

            if (targetType == ModerationTargetTypeEnum.Reply && !isReply)
            {
                throw new BadRequestException("This target is a root comment, not a reply.");
            }

            await _adminModerationRepository.DeleteCommentThreadAsync(commentId);
        }

        private static ModerationTargetTypeEnum ParseTargetType(string targetType)
        {
            if (Enum.TryParse<ModerationTargetTypeEnum>((targetType ?? string.Empty).Trim(), true, out var parsed))
            {
                return parsed;
            }

            throw new BadRequestException("Invalid moderation target type.");
        }

        private static string NormalizeKeyword(string keyword)
        {
            return (keyword ?? string.Empty).Trim();
        }

        private static string NormalizeReason(string reason)
        {
            var normalizedReason = (reason ?? string.Empty).Trim();
            if (normalizedReason.Length <= MaxReasonLength)
            {
                return normalizedReason;
            }

            return normalizedReason[..MaxReasonLength];
        }

        private static string NormalizeAction(string action)
        {
            var normalizedAction = (action ?? string.Empty).Trim().ToLowerInvariant();
            return normalizedAction;
        }

        private static string BuildAuditActionType(ModerationTargetTypeEnum targetType, string action)
        {
            return (targetType, action) switch
            {
                (ModerationTargetTypeEnum.Post, "hide") => "PostHidden",
                (ModerationTargetTypeEnum.Post, "restore") => "PostRestored",
                (ModerationTargetTypeEnum.Story, "hide") => "StoryHidden",
                (ModerationTargetTypeEnum.Story, "restore") => "StoryRestored",
                (ModerationTargetTypeEnum.Comment, "remove") => "CommentRemoved",
                (ModerationTargetTypeEnum.Reply, "remove") => "ReplyRemoved",
                _ => "ModerationAction"
            };
        }

        private static AdminModerationItemResponse MapItem(AdminModerationItemModel? item)
        {
            if (item == null)
            {
                return null!;
            }

            return new AdminModerationItemResponse
            {
                TargetId = item.TargetId,
                TargetType = item.TargetType.ToString(),
                OwnerAccountId = item.OwnerAccountId,
                OwnerUsername = item.OwnerUsername,
                OwnerFullname = item.OwnerFullname,
                OwnerEmail = item.OwnerEmail,
                LookupLabel = item.LookupLabel,
                PrimaryText = item.PrimaryText,
                SecondaryText = item.SecondaryText,
                ContentPreview = item.ContentPreview,
                CurrentState = item.CurrentState,
                IsRemoved = item.IsRemoved,
                CanRestore = item.CanRestore,
                ParentCommentId = item.ParentCommentId,
                RelatedPostId = item.RelatedPostId,
                RelatedPostCode = item.RelatedPostCode,
                CreatedAt = item.CreatedAt,
            };
        }

        private static AdminModerationItemModel BuildRemovedFallback(ModerationTargetTypeEnum targetType, Guid targetId)
        {
            return new AdminModerationItemModel
            {
                TargetId = targetId,
                TargetType = targetType,
                LookupLabel = targetId.ToString(),
                PrimaryText = targetType == ModerationTargetTypeEnum.Comment ? "comment" : "reply",
                SecondaryText = "removed",
                CurrentState = "removed",
                IsRemoved = true,
                CanRestore = false,
                CreatedAt = DateTime.UtcNow,
            };
        }
    }
}
