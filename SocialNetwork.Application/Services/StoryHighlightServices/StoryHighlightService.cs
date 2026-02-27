using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.StoryHighlightDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.AccountSettingRepos;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.StoryHighlights;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.StoryHighlightServices
{
    public class StoryHighlightService : IStoryHighlightService
    {
        private const int DefaultArchivePageSize = 20;
        private const int MaxArchivePageSize = 60;
        private const int MaxGroupsPerUser = 20;
        private const int MaxStoriesPerGroup = 50;

        private readonly IStoryHighlightRepository _storyHighlightRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IAccountSettingRepository _accountSettingRepository;
        private readonly IFollowRepository _followRepository;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IUnitOfWork _unitOfWork;

        public StoryHighlightService(
            IStoryHighlightRepository storyHighlightRepository,
            IAccountRepository accountRepository,
            IAccountSettingRepository accountSettingRepository,
            IFollowRepository followRepository,
            ICloudinaryService cloudinaryService,
            IUnitOfWork unitOfWork)
        {
            _storyHighlightRepository = storyHighlightRepository;
            _accountRepository = accountRepository;
            _accountSettingRepository = accountSettingRepository;
            _followRepository = followRepository;
            _cloudinaryService = cloudinaryService;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<StoryHighlightGroupListItemResponse>> GetProfileHighlightGroupsAsync(Guid targetAccountId, Guid? currentId)
        {
            await EnsureTargetAccountExistsAsync(targetAccountId);

            var canView = await CanCurrentViewerSeeHighlightsAsync(targetAccountId, currentId);
            if (!canView)
            {
                return new List<StoryHighlightGroupListItemResponse>();
            }

            var groups = await _storyHighlightRepository.GetHighlightGroupsByOwnerAsync(targetAccountId);
            return groups
                .Select(MapGroupListItem)
                .ToList();
        }

        public async Task<StoryHighlightGroupStoriesResponse> GetHighlightGroupStoriesAsync(Guid targetAccountId, Guid groupId, Guid? currentId)
        {
            await EnsureTargetAccountExistsAsync(targetAccountId);
            if (groupId == Guid.Empty)
            {
                throw new BadRequestException("GroupId is required.");
            }

            var canView = await CanCurrentViewerSeeHighlightsAsync(targetAccountId, currentId);
            if (!canView)
            {
                throw new NotFoundException("Highlight group not found.");
            }

            var group = await _storyHighlightRepository.GetGroupByIdAsync(groupId);
            if (group == null || group.AccountId != targetAccountId)
            {
                throw new NotFoundException("Highlight group not found.");
            }

            var stories = await _storyHighlightRepository.GetHighlightStoriesByGroupAsync(groupId, currentId);
            if (stories.Count == 0)
            {
                throw new NotFoundException("Highlight group not found.");
            }

            return new StoryHighlightGroupStoriesResponse
            {
                StoryHighlightGroupId = group.StoryHighlightGroupId,
                AccountId = group.AccountId,
                Name = group.Name,
                CoverImageUrl = group.CoverImageUrl,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt,
                Stories = stories.Select(MapStoryItem).ToList()
            };
        }

        public async Task<PagedResponse<StoryHighlightArchiveCandidateResponse>> GetArchiveCandidatesAsync(
            Guid currentId,
            int page,
            int pageSize,
            Guid? excludeGroupId)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = DefaultArchivePageSize;
            if (pageSize > MaxArchivePageSize) pageSize = MaxArchivePageSize;

            if (excludeGroupId.HasValue && excludeGroupId.Value != Guid.Empty)
            {
                var group = await _storyHighlightRepository.GetGroupByIdByOwnerAsync(excludeGroupId.Value, currentId);
                if (group == null)
                {
                    throw new NotFoundException("Highlight group not found.");
                }

                var effectiveStoryCount = await _storyHighlightRepository.CountEffectiveStoriesInGroupAsync(excludeGroupId.Value);
                if (effectiveStoryCount <= 0)
                {
                    throw new NotFoundException("Highlight group not found.");
                }
            }

            var (items, totalItems) = await _storyHighlightRepository.GetArchiveCandidatesAsync(
                currentId,
                DateTime.UtcNow,
                page,
                pageSize,
                excludeGroupId);

            var responses = items.Select(MapArchiveCandidate).ToList();
            return new PagedResponse<StoryHighlightArchiveCandidateResponse>(responses, page, pageSize, totalItems);
        }

        public async Task<StoryHighlightGroupMutationResponse> CreateGroupAsync(Guid currentId, StoryHighlightCreateGroupRequest request)
        {
            if (request == null)
            {
                throw new BadRequestException("Request is required.");
            }

            var normalizedName = NormalizeGroupName(request.Name);
            var normalizedStoryIds = NormalizeStoryIds(request.StoryIds);
            if (normalizedStoryIds.Count == 0)
            {
                throw new BadRequestException("At least one archive story must be selected.");
            }

            if (normalizedStoryIds.Count > MaxStoriesPerGroup)
            {
                throw new BadRequestException($"Maximum {MaxStoriesPerGroup} stories are allowed per request.");
            }

            var currentGroupCount = await _storyHighlightRepository.CountGroupsByOwnerAsync(currentId);
            if (currentGroupCount >= MaxGroupsPerUser)
            {
                throw new BadRequestException($"Maximum {MaxGroupsPerUser} highlight groups are allowed.");
            }

            var nowUtc = DateTime.UtcNow;
            var selectedStories = await _storyHighlightRepository.GetArchiveStoriesByIdsForOwnerAsync(currentId, normalizedStoryIds, nowUtc);
            if (selectedStories.Count != normalizedStoryIds.Count)
            {
                throw new BadRequestException("Some selected stories are invalid or no longer available in archive.");
            }

            string? uploadedCoverUrl = null;
            if (request.CoverImageFile != null && request.CoverImageFile.Length > 0)
            {
                uploadedCoverUrl = await _cloudinaryService.UploadImageAsync(request.CoverImageFile);
                if (string.IsNullOrWhiteSpace(uploadedCoverUrl))
                {
                    throw new BadRequestException("Failed to upload highlight cover image.");
                }
            }

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var group = new StoryHighlightGroup
                {
                    StoryHighlightGroupId = Guid.NewGuid(),
                    AccountId = currentId,
                    Name = normalizedName,
                    CoverImageUrl = uploadedCoverUrl,
                    CreatedAt = nowUtc,
                    UpdatedAt = null
                };

                var orderedStories = selectedStories
                    .OrderBy(s => s.CreatedAt)
                    .ThenBy(s => s.StoryId)
                    .ToList();

                var items = orderedStories.Select(story => new StoryHighlightItem
                {
                    StoryHighlightGroupId = group.StoryHighlightGroupId,
                    StoryId = story.StoryId,
                    AddedAt = nowUtc
                }).ToList();

                await _storyHighlightRepository.AddGroupAsync(group);
                await _storyHighlightRepository.AddItemsAsync(items);

                return new StoryHighlightGroupMutationResponse
                {
                    StoryHighlightGroupId = group.StoryHighlightGroupId,
                    StoryCount = items.Count,
                    Name = group.Name,
                    CoverImageUrl = group.CoverImageUrl,
                    CreatedAt = group.CreatedAt,
                    UpdatedAt = group.UpdatedAt
                };
            }, async () =>
            {
                if (string.IsNullOrWhiteSpace(uploadedCoverUrl))
                {
                    return;
                }

                var publicId = _cloudinaryService.GetPublicIdFromUrl(uploadedCoverUrl);
                if (string.IsNullOrWhiteSpace(publicId))
                {
                    return;
                }

                await _cloudinaryService.DeleteMediaAsync(publicId, MediaTypeEnum.Image);
            });
        }

        public async Task<StoryHighlightGroupMutationResponse> AddItemsAsync(Guid currentId, Guid groupId, StoryHighlightAddItemsRequest request)
        {
            if (request == null)
            {
                throw new BadRequestException("Request is required.");
            }

            if (groupId == Guid.Empty)
            {
                throw new BadRequestException("GroupId is required.");
            }

            var normalizedStoryIds = NormalizeStoryIds(request.StoryIds);
            if (normalizedStoryIds.Count == 0)
            {
                throw new BadRequestException("At least one archive story must be selected.");
            }

            var group = await _storyHighlightRepository.GetGroupByIdByOwnerAsync(groupId, currentId);
            if (group == null)
            {
                throw new NotFoundException("Highlight group not found.");
            }

            var currentStoryCount = await _storyHighlightRepository.CountEffectiveStoriesInGroupAsync(groupId);
            if (currentStoryCount <= 0)
            {
                throw new NotFoundException("Highlight group not found.");
            }

            var existingStoryIds = await _storyHighlightRepository.GetExistingStoryIdsInGroupAsync(groupId, normalizedStoryIds);
            if (existingStoryIds.Count > 0)
            {
                throw new BadRequestException("Some selected stories already exist in this highlight group.");
            }

            var nowUtc = DateTime.UtcNow;
            var selectedStories = await _storyHighlightRepository.GetArchiveStoriesByIdsForOwnerAsync(currentId, normalizedStoryIds, nowUtc);
            if (selectedStories.Count != normalizedStoryIds.Count)
            {
                throw new BadRequestException("Some selected stories are invalid or no longer available in archive.");
            }

            if (currentStoryCount + selectedStories.Count > MaxStoriesPerGroup)
            {
                throw new BadRequestException($"Maximum {MaxStoriesPerGroup} stories are allowed in a group.");
            }

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var orderedStories = selectedStories
                    .OrderBy(s => s.CreatedAt)
                    .ThenBy(s => s.StoryId)
                    .ToList();

                var items = orderedStories.Select(story => new StoryHighlightItem
                {
                    StoryHighlightGroupId = groupId,
                    StoryId = story.StoryId,
                    AddedAt = nowUtc
                }).ToList();

                await _storyHighlightRepository.AddItemsAsync(items);

                group.UpdatedAt = nowUtc;
                await _storyHighlightRepository.UpdateGroupAsync(group);

                return new StoryHighlightGroupMutationResponse
                {
                    StoryHighlightGroupId = group.StoryHighlightGroupId,
                    StoryCount = currentStoryCount + items.Count,
                    Name = group.Name,
                    CoverImageUrl = group.CoverImageUrl,
                    CreatedAt = group.CreatedAt,
                    UpdatedAt = group.UpdatedAt
                };
            });
        }

        public async Task<StoryHighlightGroupMutationResponse> UpdateGroupAsync(Guid currentId, Guid groupId, StoryHighlightUpdateGroupRequest request)
        {
            if (request == null)
            {
                throw new BadRequestException("Request is required.");
            }

            if (groupId == Guid.Empty)
            {
                throw new BadRequestException("GroupId is required.");
            }

            var group = await _storyHighlightRepository.GetGroupByIdByOwnerAsync(groupId, currentId);
            if (group == null)
            {
                throw new NotFoundException("Highlight group not found.");
            }

            var currentCount = await _storyHighlightRepository.CountEffectiveStoriesInGroupAsync(groupId);
            if (currentCount <= 0)
            {
                throw new NotFoundException("Highlight group not found.");
            }

            var hasNameUpdate = request.Name != null;
            var hasCoverUpload = request.CoverImageFile != null && request.CoverImageFile.Length > 0;
            var removeCover = request.RemoveCoverImage == true;

            if (hasCoverUpload && removeCover)
            {
                throw new BadRequestException("Cannot upload and remove cover image at the same time.");
            }

            var normalizedName = hasNameUpdate ? NormalizeGroupName(request.Name) : group.Name;
            var hasAnyChange = hasNameUpdate || hasCoverUpload || removeCover;
            if (!hasAnyChange)
            {
                return new StoryHighlightGroupMutationResponse
                {
                    StoryHighlightGroupId = group.StoryHighlightGroupId,
                    StoryCount = currentCount,
                    Name = group.Name,
                    CoverImageUrl = group.CoverImageUrl,
                    CreatedAt = group.CreatedAt,
                    UpdatedAt = group.UpdatedAt
                };
            }

            var oldCoverUrl = group.CoverImageUrl;
            var oldCoverPublicId = string.IsNullOrWhiteSpace(oldCoverUrl)
                ? null
                : _cloudinaryService.GetPublicIdFromUrl(oldCoverUrl);

            string? uploadedCoverUrl = null;
            if (hasCoverUpload)
            {
                uploadedCoverUrl = await _cloudinaryService.UploadImageAsync(request.CoverImageFile!);
                if (string.IsNullOrWhiteSpace(uploadedCoverUrl))
                {
                    throw new BadRequestException("Failed to upload highlight cover image.");
                }
            }

            var nowUtc = DateTime.UtcNow;
            var response = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                if (hasNameUpdate)
                {
                    group.Name = normalizedName;
                }

                if (removeCover)
                {
                    group.CoverImageUrl = null;
                }
                else if (!string.IsNullOrWhiteSpace(uploadedCoverUrl))
                {
                    group.CoverImageUrl = uploadedCoverUrl;
                }

                group.UpdatedAt = nowUtc;
                await _storyHighlightRepository.UpdateGroupAsync(group);

                var currentCount = await _storyHighlightRepository.CountEffectiveStoriesInGroupAsync(groupId);
                return new StoryHighlightGroupMutationResponse
                {
                    StoryHighlightGroupId = group.StoryHighlightGroupId,
                    StoryCount = currentCount,
                    Name = group.Name,
                    CoverImageUrl = group.CoverImageUrl,
                    CreatedAt = group.CreatedAt,
                    UpdatedAt = group.UpdatedAt
                };
            }, async () =>
            {
                if (string.IsNullOrWhiteSpace(uploadedCoverUrl))
                {
                    return;
                }

                var uploadedPublicId = _cloudinaryService.GetPublicIdFromUrl(uploadedCoverUrl);
                if (string.IsNullOrWhiteSpace(uploadedPublicId))
                {
                    return;
                }

                await _cloudinaryService.DeleteMediaAsync(uploadedPublicId, MediaTypeEnum.Image);
            });

            var coverChanged = hasCoverUpload || removeCover;
            if (coverChanged && !string.IsNullOrWhiteSpace(oldCoverPublicId))
            {
                await _cloudinaryService.DeleteMediaAsync(oldCoverPublicId, MediaTypeEnum.Image);
            }

            return response;
        }

        public async Task RemoveItemAsync(Guid currentId, Guid groupId, Guid storyId)
        {
            if (storyId == Guid.Empty)
            {
                throw new BadRequestException("StoryId is required.");
            }

            var group = await _storyHighlightRepository.GetGroupByIdByOwnerAsync(groupId, currentId);
            if (group == null)
            {
                throw new NotFoundException("Highlight group not found.");
            }

            var existingStoryIds = await _storyHighlightRepository.GetExistingStoryIdsInGroupAsync(groupId, new[] { storyId });
            if (!existingStoryIds.Contains(storyId))
            {
                throw new NotFoundException("Highlight story item not found.");
            }

            var effectiveStoryCount = await _storyHighlightRepository.CountEffectiveStoriesInGroupAsync(groupId);
            if (effectiveStoryCount <= 0)
            {
                throw new NotFoundException("Highlight story item not found.");
            }

            var coverPublicId = !string.IsNullOrWhiteSpace(group.CoverImageUrl)
                ? _cloudinaryService.GetPublicIdFromUrl(group.CoverImageUrl)
                : null;
            var deletedGroup = false;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _storyHighlightRepository.RemoveItemAsync(groupId, storyId);
                await _unitOfWork.CommitAsync();

                deletedGroup = await _storyHighlightRepository.TryRemoveGroupIfEffectivelyEmptyAsync(groupId, currentId);
                if (deletedGroup)
                {
                    return true;
                }

                group.UpdatedAt = DateTime.UtcNow;
                await _storyHighlightRepository.UpdateGroupAsync(group);

                return true;
            });

            if (deletedGroup && !string.IsNullOrWhiteSpace(coverPublicId))
            {
                await _cloudinaryService.DeleteMediaAsync(coverPublicId, MediaTypeEnum.Image);
            }
        }

        public async Task DeleteGroupAsync(Guid currentId, Guid groupId)
        {
            if (groupId == Guid.Empty)
            {
                throw new BadRequestException("GroupId is required.");
            }

            var group = await _storyHighlightRepository.GetGroupByIdByOwnerAsync(groupId, currentId);
            if (group == null)
            {
                throw new NotFoundException("Highlight group not found.");
            }

            var effectiveStoryCount = await _storyHighlightRepository.CountEffectiveStoriesInGroupAsync(groupId);
            if (effectiveStoryCount <= 0)
            {
                throw new NotFoundException("Highlight group not found.");
            }

            var coverPublicId = string.IsNullOrWhiteSpace(group.CoverImageUrl)
                ? null
                : _cloudinaryService.GetPublicIdFromUrl(group.CoverImageUrl);

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _storyHighlightRepository.RemoveGroupAsync(group);
                return true;
            });

            if (!string.IsNullOrWhiteSpace(coverPublicId))
            {
                await _cloudinaryService.DeleteMediaAsync(coverPublicId, MediaTypeEnum.Image);
            }
        }

        private async Task EnsureTargetAccountExistsAsync(Guid targetAccountId)
        {
            if (targetAccountId == Guid.Empty)
            {
                throw new BadRequestException("Target account is required.");
            }

            var exists = await _accountRepository.IsAccountIdExist(targetAccountId);
            if (!exists)
            {
                throw new NotFoundException("Account not found.");
            }
        }

        private async Task<bool> CanCurrentViewerSeeHighlightsAsync(Guid targetAccountId, Guid? currentId)
        {
            if (currentId.HasValue && currentId.Value == targetAccountId)
            {
                return true;
            }

            var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(targetAccountId);
            var highlightPrivacy = settings?.StoryHighlightPrivacy ?? AccountPrivacyEnum.Public;

            if (highlightPrivacy == AccountPrivacyEnum.Public)
            {
                return true;
            }

            if (highlightPrivacy == AccountPrivacyEnum.Private)
            {
                return false;
            }

            if (!currentId.HasValue)
            {
                return false;
            }

            return await _followRepository.IsFollowingAsync(currentId.Value, targetAccountId);
        }

        private static string NormalizeGroupName(string? rawName)
        {
            var normalizedName = (rawName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new BadRequestException("Group name is required.");
            }

            if (normalizedName.Length > 50)
            {
                throw new BadRequestException("Group name must be at most 50 characters.");
            }

            return normalizedName;
        }

        private static List<Guid> NormalizeStoryIds(List<Guid>? storyIds)
        {
            return (storyIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
        }

        private static StoryHighlightGroupListItemResponse MapGroupListItem(StoryHighlightGroupListItemModel model)
        {
            return new StoryHighlightGroupListItemResponse
            {
                StoryHighlightGroupId = model.StoryHighlightGroupId,
                AccountId = model.AccountId,
                Name = model.Name,
                CoverImageUrl = model.CoverImageUrl,
                StoryCount = model.StoryCount,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt,
                FallbackStory = model.FallbackStory != null ? MapArchiveCandidate(model.FallbackStory) : null
            };
        }

        private static StoryHighlightArchiveCandidateResponse MapArchiveCandidate(StoryHighlightArchiveCandidateModel model)
        {
            return new StoryHighlightArchiveCandidateResponse
            {
                StoryId = model.StoryId,
                ContentType = (int)model.ContentType,
                MediaUrl = model.MediaUrl,
                TextContent = model.TextContent,
                BackgroundColorKey = model.BackgroundColorKey,
                FontTextKey = model.FontTextKey,
                FontSizeKey = model.FontSizeKey,
                TextColorKey = model.TextColorKey,
                CreatedAt = model.CreatedAt,
                ExpiresAt = model.ExpiresAt
            };
        }

        private static StoryHighlightStoryItemResponse MapStoryItem(StoryHighlightStoryItemModel model)
        {
            return new StoryHighlightStoryItemResponse
            {
                StoryId = model.StoryId,
                ContentType = (int)model.ContentType,
                MediaUrl = model.MediaUrl,
                TextContent = model.TextContent,
                BackgroundColorKey = model.BackgroundColorKey,
                FontTextKey = model.FontTextKey,
                FontSizeKey = model.FontSizeKey,
                TextColorKey = model.TextColorKey,
                CreatedAt = model.CreatedAt,
                ExpiresAt = model.ExpiresAt,
                IsViewedByCurrentUser = model.IsViewedByCurrentUser,
                CurrentUserReactType = model.CurrentUserReactType.HasValue
                    ? (int)model.CurrentUserReactType.Value
                    : null
            };
        }
    }
}
