using AutoMapper;
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
        private readonly IMapper _mapper;

        public StoryService(
            IStoryRepository storyRepository,
            IAccountRepository accountRepository,
            ICloudinaryService cloudinaryService,
            IFileTypeDetector fileTypeDetector,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _storyRepository = storyRepository;
            _accountRepository = accountRepository;
            _cloudinaryService = cloudinaryService;
            _fileTypeDetector = fileTypeDetector;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
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

                if (!string.IsNullOrWhiteSpace(request.BackgroundColorKey)
                    || !string.IsNullOrWhiteSpace(request.FontTextKey)
                    || !string.IsNullOrWhiteSpace(request.FontSizeKey)
                    || !string.IsNullOrWhiteSpace(request.TextColorKey))
                {
                    throw new BadRequestException("Text style keys are only allowed for text story.");
                }
            }

            string? uploadedMediaUrl = null;
            MediaTypeEnum? uploadedMediaType = null;
            if (contentType != StoryContentTypeEnum.Text)
            {
                var mediaFile = request.MediaFile!;
                var detectedType = await _fileTypeDetector.GetMediaTypeAsync(mediaFile);
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
                    ? await _cloudinaryService.UploadImageAsync(mediaFile)
                    : await _cloudinaryService.UploadVideoAsync(mediaFile);

                if (string.IsNullOrWhiteSpace(uploadedMediaUrl))
                {
                    throw new BadRequestException("Failed to upload story media.");
                }
            }

            var normalizedBackgroundColorKey = string.IsNullOrWhiteSpace(request.BackgroundColorKey)
                ? null
                : request.BackgroundColorKey.Trim();
            var normalizedFontTextKey = string.IsNullOrWhiteSpace(request.FontTextKey)
                ? null
                : request.FontTextKey.Trim();
            var normalizedFontSizeKey = string.IsNullOrWhiteSpace(request.FontSizeKey)
                ? null
                : request.FontSizeKey.Trim();
            var normalizedTextColorKey = string.IsNullOrWhiteSpace(request.TextColorKey)
                ? null
                : request.TextColorKey.Trim();
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
                    TextContent = contentType == StoryContentTypeEnum.Text ? normalizedTextContent : null,
                    BackgroundColorKey = contentType == StoryContentTypeEnum.Text ? normalizedBackgroundColorKey : null,
                    FontTextKey = contentType == StoryContentTypeEnum.Text ? normalizedFontTextKey : null,
                    FontSizeKey = contentType == StoryContentTypeEnum.Text ? normalizedFontSizeKey : null,
                    TextColorKey = contentType == StoryContentTypeEnum.Text ? normalizedTextColorKey : null,
                    Privacy = privacy,
                    ExpiresAt = now.AddHours((int)expiresEnum),
                    CreatedAt = now,
                    IsDeleted = false
                };

                await _storyRepository.AddStoryAsync(story);
                return _mapper.Map<StoryDetailResponse>(story);
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

        public async Task<StoryDetailResponse> UpdateStoryPrivacyAsync(Guid storyId, Guid currentId, StoryPrivacyUpdateRequest request)
        {
            if (!request.Privacy.HasValue || !Enum.IsDefined(typeof(StoryPrivacyEnum), request.Privacy.Value))
            {
                throw new BadRequestException("Invalid story privacy setting.");
            }

            var story = await _storyRepository.GetStoryByIdAsync(storyId);
            if (story == null || story.IsDeleted)
            {
                throw new NotFoundException($"Story with ID {storyId} not found.");
            }

            if (story.AccountId != currentId)
            {
                throw new ForbiddenException("You are not authorized to edit this story.");
            }

            if (story.ExpiresAt <= DateTime.UtcNow)
            {
                throw new BadRequestException("Story has expired and cannot be edited.");
            }

            var privacy = (StoryPrivacyEnum)request.Privacy.Value;
            if (story.Privacy == privacy)
            {
                return _mapper.Map<StoryDetailResponse>(story);
            }

            story.Privacy = privacy;
            await _storyRepository.UpdateStoryAsync(story);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<StoryDetailResponse>(story);
        }

        public async Task SoftDeleteStoryAsync(Guid storyId, Guid currentId)
        {
            var story = await _storyRepository.GetStoryByIdAsync(storyId);
            if (story == null || story.IsDeleted)
            {
                throw new NotFoundException($"Story with ID {storyId} not found.");
            }

            if (story.AccountId != currentId)
            {
                throw new ForbiddenException("You are not authorized to delete this story.");
            }

            story.IsDeleted = true;
            await _storyRepository.UpdateStoryAsync(story);
            await _unitOfWork.CommitAsync();
        }
    }
}
