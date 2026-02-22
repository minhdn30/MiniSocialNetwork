using SocialNetwork.Application.DTOs.StoryDTOs;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.Stories;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.StoryServices
{
    public class StoryService : IStoryService
    {
        private readonly IStoryRepository _storyRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IFileTypeDetector _fileTypeDetector;
        private readonly IUnitOfWork _unitOfWork;

        public StoryService(
            IStoryRepository storyRepository,
            IAccountRepository accountRepository,
            ICloudinaryService cloudinaryService,
            IFileTypeDetector fileTypeDetector,
            IUnitOfWork unitOfWork)
        {
            _storyRepository = storyRepository;
            _accountRepository = accountRepository;
            _cloudinaryService = cloudinaryService;
            _fileTypeDetector = fileTypeDetector;
            _unitOfWork = unitOfWork;
        }

        public async Task<StoryDetailResponse> CreateStoryAsync(Guid currentId, StoryCreateRequest request)
        {
            var account = await _accountRepository.GetAccountById(currentId);
            if (account == null)
            {
                throw new BadRequestException($"Account with ID {currentId} not found.");
            }

            if (account.Status != AccountStatusEnum.Active)
            {
                throw new ForbiddenException("You must reactivate your account to create stories.");
            }

            var now = DateTime.UtcNow;
            var contentType = (StoryContentTypeEnum)request.ContentType!.Value;
            var privacy = (StoryPrivacyEnum)(request.Privacy ?? (int)StoryPrivacyEnum.Public);
            var expiresEnum = request.ExpiresEnum.HasValue && Enum.IsDefined(typeof(StoryExpiresEnum), request.ExpiresEnum.Value)
                ? (StoryExpiresEnum)request.ExpiresEnum.Value
                : StoryExpiresEnum.Hours24;

            if (contentType == StoryContentTypeEnum.Text)
            {
                if (string.IsNullOrWhiteSpace(request.TextContent))
                {
                    throw new BadRequestException("TextContent is required for text story.");
                }

                if (request.MediaFile != null)
                {
                    throw new BadRequestException("MediaFile is not allowed for text story.");
                }

                if (!string.IsNullOrWhiteSpace(request.ThumbnailUrl))
                {
                    throw new BadRequestException("ThumbnailUrl is not allowed for text story.");
                }
            }
            else
            {
                if (request.MediaFile == null || request.MediaFile.Length == 0)
                {
                    throw new BadRequestException("MediaFile is required for image/video story.");
                }

                if (!string.IsNullOrWhiteSpace(request.TextContent))
                {
                    throw new BadRequestException("TextContent is only allowed for text story.");
                }
            }

            string? uploadedMediaUrl = null;
            MediaTypeEnum? uploadedMediaType = null;
            if (contentType != StoryContentTypeEnum.Text)
            {
                if (request.MediaFile == null)
                {
                    throw new BadRequestException("MediaFile is required for image/video story.");
                }

                var detectedType = await _fileTypeDetector.GetMediaTypeAsync(request.MediaFile);
                if (contentType == StoryContentTypeEnum.Image && detectedType != MediaTypeEnum.Image)
                {
                    throw new BadRequestException("MediaFile must be an image for image story.");
                }

                if (contentType == StoryContentTypeEnum.Video && detectedType != MediaTypeEnum.Video)
                {
                    throw new BadRequestException("MediaFile must be a video for video story.");
                }

                uploadedMediaType = detectedType;
                uploadedMediaUrl = contentType == StoryContentTypeEnum.Image
                    ? await _cloudinaryService.UploadImageAsync(request.MediaFile)
                    : await _cloudinaryService.UploadVideoAsync(request.MediaFile);

                if (string.IsNullOrWhiteSpace(uploadedMediaUrl))
                {
                    throw new BadRequestException("Failed to upload story media.");
                }
            }

            var normalizedThumbnailUrl = string.IsNullOrWhiteSpace(request.ThumbnailUrl)
                ? null
                : request.ThumbnailUrl.Trim();
            var normalizedTextContent = string.IsNullOrWhiteSpace(request.TextContent)
                ? null
                : request.TextContent.Trim();

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var story = new Story
                {
                    StoryId = Guid.NewGuid(),
                    AccountId = currentId,
                    ContentType = contentType,
                    MediaUrl = contentType == StoryContentTypeEnum.Text ? null : uploadedMediaUrl,
                    ThumbnailUrl = contentType == StoryContentTypeEnum.Text ? null : normalizedThumbnailUrl,
                    TextContent = contentType == StoryContentTypeEnum.Text ? normalizedTextContent : null,
                    Privacy = privacy,
                    ExpiresAt = now.AddHours((int)expiresEnum),
                    CreatedAt = now,
                    IsDeleted = false
                };

                await _storyRepository.AddStoryAsync(story);
                await _unitOfWork.CommitAsync();

                return new StoryDetailResponse
                {
                    StoryId = story.StoryId,
                    AccountId = story.AccountId,
                    ContentType = (int)story.ContentType,
                    MediaUrl = story.MediaUrl,
                    ThumbnailUrl = story.ThumbnailUrl,
                    TextContent = story.TextContent,
                    Privacy = (int)story.Privacy,
                    CreatedAt = story.CreatedAt,
                    ExpiresAt = story.ExpiresAt,
                    IsDeleted = story.IsDeleted
                };
            }, async () =>
            {
                if (string.IsNullOrWhiteSpace(uploadedMediaUrl) || !uploadedMediaType.HasValue)
                {
                    return;
                }

                var publicId = _cloudinaryService.GetPublicIdFromUrl(uploadedMediaUrl);
                if (string.IsNullOrWhiteSpace(publicId))
                {
                    return;
                }

                await _cloudinaryService.DeleteMediaAsync(publicId, uploadedMediaType.Value);
            });
        }
    }
}
