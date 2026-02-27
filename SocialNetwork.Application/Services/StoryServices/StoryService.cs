using AutoMapper;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.StoryDTOs;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.Stories;
using SocialNetwork.Infrastructure.Repositories.StoryViews;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.StoryServices
{
    public class StoryService : IStoryService
    {
        private const int DefaultAuthorsPageSize = 20;
        private const int MaxAuthorsPageSize = 50;
        private const int DefaultArchivePageSize = 20;
        private const int MaxArchivePageSize = 60;
        private const int DefaultTopViewersCount = 3;

        private readonly IStoryRepository _storyRepository;
        private readonly IStoryViewRepository _storyViewRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IFileTypeDetector _fileTypeDetector;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public StoryService(
            IStoryRepository storyRepository,
            IStoryViewRepository storyViewRepository,
            IAccountRepository accountRepository,
            ICloudinaryService cloudinaryService,
            IFileTypeDetector fileTypeDetector,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _storyRepository = storyRepository;
            _storyViewRepository = storyViewRepository;
            _accountRepository = accountRepository;
            _cloudinaryService = cloudinaryService;
            _fileTypeDetector = fileTypeDetector;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<PagedResponse<StoryAuthorItemResponse>> GetViewableAuthorsAsync(Guid currentId, int page, int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = DefaultAuthorsPageSize;
            if (pageSize > MaxAuthorsPageSize) pageSize = MaxAuthorsPageSize;

            var (items, totalItems) = await _storyRepository.GetViewableAuthorSummariesAsync(
                currentId,
                DateTime.UtcNow,
                page,
                pageSize);

            var responses = items.Select(item => new StoryAuthorItemResponse
            {
                AccountId = item.AccountId,
                Username = item.Username,
                FullName = item.FullName,
                AvatarUrl = item.AvatarUrl,
                LatestStoryCreatedAt = item.LatestStoryCreatedAt,
                ActiveStoryCount = item.ActiveStoryCount,
                UnseenCount = item.UnseenCount,
                StoryRingState = item.ActiveStoryCount <= 0
                    ? StoryRingStateEnum.None
                    : (item.UnseenCount > 0 ? StoryRingStateEnum.Unseen : StoryRingStateEnum.Seen),
                IsCurrentUser = item.AccountId == currentId
            }).ToList();

            return new PagedResponse<StoryAuthorItemResponse>(responses, page, pageSize, totalItems);
        }

        public async Task<PagedResponse<StoryArchiveItemResponse>> GetArchivedStoriesAsync(Guid currentId, int page, int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = DefaultArchivePageSize;
            if (pageSize > MaxArchivePageSize) pageSize = MaxArchivePageSize;

            var (items, totalItems) = await _storyRepository.GetArchivedStoriesByOwnerAsync(
                currentId,
                DateTime.UtcNow,
                page,
                pageSize);

            if (items.Count == 0)
            {
                return new PagedResponse<StoryArchiveItemResponse>(Array.Empty<StoryArchiveItemResponse>(), page, pageSize, totalItems);
            }

            var storyIds = items.Select(x => x.StoryId).Distinct().ToList();
            var viewSummaryMap = await _storyViewRepository.GetStoryViewSummariesAsync(
                currentId,
                storyIds,
                topCount: 1);

            var responses = items.Select(item =>
            {
                viewSummaryMap.TryGetValue(item.StoryId, out var summary);
                return new StoryArchiveItemResponse
                {
                    StoryId = item.StoryId,
                    ContentType = (int)item.ContentType,
                    MediaUrl = item.MediaUrl,
                    TextContent = item.TextContent,
                    BackgroundColorKey = item.BackgroundColorKey,
                    FontTextKey = item.FontTextKey,
                    FontSizeKey = item.FontSizeKey,
                    TextColorKey = item.TextColorKey,
                    Privacy = (int)item.Privacy,
                    CreatedAt = item.CreatedAt,
                    ExpiresAt = item.ExpiresAt,
                    ViewCount = summary?.TotalViews ?? 0,
                    ReactCount = summary?.TotalReacts ?? 0
                };
            }).ToList();

            return new PagedResponse<StoryArchiveItemResponse>(responses, page, pageSize, totalItems);
        }

        public async Task<StoryAuthorActiveResponse> GetActiveStoriesByAuthorAsync(Guid currentId, Guid authorId)
        {
            if (authorId == Guid.Empty)
            {
                throw new BadRequestException("AuthorId is required.");
            }

            var storyItems = await _storyRepository.GetActiveStoriesByAuthorAsync(currentId, authorId, DateTime.UtcNow);
            if (storyItems.Count == 0)
            {
                throw new NotFoundException("No active stories found or access is denied.");
            }

            var firstItem = storyItems[0];
            var stories = storyItems.Select(item => new StoryActiveItemResponse
            {
                StoryId = item.StoryId,
                ContentType = (int)item.ContentType,
                MediaUrl = item.MediaUrl,
                TextContent = item.TextContent,
                BackgroundColorKey = item.BackgroundColorKey,
                FontTextKey = item.FontTextKey,
                FontSizeKey = item.FontSizeKey,
                TextColorKey = item.TextColorKey,
                Privacy = (int)item.Privacy,
                CreatedAt = item.CreatedAt,
                ExpiresAt = item.ExpiresAt,
                IsViewedByCurrentUser = item.IsViewedByCurrentUser,
                CurrentUserReactType = item.CurrentUserReactType.HasValue ? (int)item.CurrentUserReactType.Value : null
            }).ToList();

            if (authorId == currentId)
            {
                var storyIds = storyItems.Select(x => x.StoryId).Distinct().ToList();
                var viewSummaryMap = await _storyViewRepository.GetStoryViewSummariesAsync(
                    authorId,
                    storyIds,
                    DefaultTopViewersCount);

                foreach (var story in stories)
                {
                    if (!viewSummaryMap.TryGetValue(story.StoryId, out var summary))
                    {
                        story.ViewSummary = new StoryViewSummaryResponse
                        {
                            TotalViews = 0,
                            TopViewers = Array.Empty<StoryViewerBasicResponse>()
                        };
                        continue;
                    }

                    story.ViewSummary = new StoryViewSummaryResponse
                    {
                        TotalViews = summary.TotalViews,
                        TopViewers = summary.TopViewers
                            .Select(v => new StoryViewerBasicResponse
                            {
                                AccountId = v.AccountId,
                                Username = v.Username,
                                FullName = v.FullName,
                                AvatarUrl = v.AvatarUrl,
                                ViewedAt = v.ViewedAt,
                                ReactType = v.ReactType.HasValue ? (int)v.ReactType.Value : null
                            })
                            .ToList()
                    };
                }
            }

            return new StoryAuthorActiveResponse
            {
                AccountId = firstItem.AccountId,
                Username = firstItem.Username,
                FullName = firstItem.FullName,
                AvatarUrl = firstItem.AvatarUrl,
                Stories = stories
            };
        }

        public async Task<StoryResolveResponse?> ResolveStoryAsync(Guid currentId, Guid storyId)
        {
            if (storyId == Guid.Empty)
            {
                throw new BadRequestException("StoryId is required.");
            }

            var authorId = await _storyRepository.ResolveAuthorIdByStoryIdAsync(
                currentId,
                storyId,
                DateTime.UtcNow);

            if (!authorId.HasValue)
            {
                return null;
            }

            return new StoryResolveResponse
            {
                StoryId = storyId,
                AuthorId = authorId.Value
            };
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

            // Anti-duplicate check (10 seconds window)
            var contentType = (StoryContentTypeEnum)request.ContentType!.Value;
            bool isDuplicate = await _storyRepository.HasRecentStoryAsync(currentId, contentType, TimeSpan.FromSeconds(10));
            if (isDuplicate)
            {
                throw new BadRequestException("You are posting stories too fast. Please Wait a few seconds.");
            }

            var now = DateTime.UtcNow;
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

                if (!string.IsNullOrWhiteSpace(request.FontTextKey)
                    || !string.IsNullOrWhiteSpace(request.FontSizeKey)
                    || !string.IsNullOrWhiteSpace(request.TextColorKey))
                {
                    throw new BadRequestException("Font, size and text color keys are only allowed for text story.");
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
                    BackgroundColorKey = normalizedBackgroundColorKey,
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
