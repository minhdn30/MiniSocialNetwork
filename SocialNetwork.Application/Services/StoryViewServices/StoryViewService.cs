using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.StoryDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Stories;
using SocialNetwork.Infrastructure.Repositories.StoryViews;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using SocialNetwork.Infrastructure.Models;
using AutoMapper;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.StoryViewServices
{
    public class StoryViewService : IStoryViewService
    {
        private const int DefaultViewersPageSize = 20;
        private const int MaxMarkViewedStoryIds = 200;

        private readonly IStoryViewRepository _storyViewRepository;
        private readonly IStoryRepository _storyRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public StoryViewService(
            IStoryViewRepository storyViewRepository,
            IStoryRepository storyRepository,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _storyViewRepository = storyViewRepository;
            _storyRepository = storyRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<StoryMarkViewedResponse> MarkStoriesViewedAsync(Guid currentId, StoryMarkViewedRequest request)
        {
            if (request?.StoryIds == null || request.StoryIds.Count == 0)
            {
                throw new BadRequestException("StoryIds is required.");
            }

            if (request.StoryIds.Count > MaxMarkViewedStoryIds)
            {
                throw new BadRequestException($"Maximum {MaxMarkViewedStoryIds} storyIds are allowed per request.");
            }

            var normalizedStoryIds = request.StoryIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedStoryIds.Count == 0)
            {
                throw new BadRequestException("StoryIds is invalid.");
            }

            var nowUtc = DateTime.UtcNow;
            var visibleStoryIds = await _storyRepository.GetViewableStoryIdsAsync(currentId, normalizedStoryIds, nowUtc);
            if (visibleStoryIds.Count == 0)
            {
                return new StoryMarkViewedResponse
                {
                    RequestedCount = normalizedStoryIds.Count,
                    VisibleCount = 0,
                    MarkedCount = 0
                };
            }

            var existingViewedStoryIds = await _storyViewRepository.GetViewedStoryIdsByViewerAsync(currentId, visibleStoryIds);
            var toInsert = visibleStoryIds
                .Where(id => !existingViewedStoryIds.Contains(id))
                .Select(id => new StoryView
                {
                    StoryId = id,
                    ViewerAccountId = currentId,
                    ViewedAt = nowUtc
                })
                .ToList();

            var markedCount = 0;
            if (toInsert.Count > 0)
            {
                markedCount = await _storyViewRepository.AddStoryViewsIgnoreConflictAsync(toInsert);
            }

            return new StoryMarkViewedResponse
            {
                RequestedCount = normalizedStoryIds.Count,
                VisibleCount = visibleStoryIds.Count,
                MarkedCount = markedCount
            };
        }

        public async Task<IReadOnlyDictionary<Guid, StoryRingStateEnum>> GetStoryRingStatesForAuthorsAsync(
            Guid currentId,
            IEnumerable<Guid> authorIds)
        {
            var normalizedAuthorIds = (authorIds ?? Enumerable.Empty<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedAuthorIds.Count == 0)
            {
                return new Dictionary<Guid, StoryRingStateEnum>();
            }

            var result = normalizedAuthorIds.ToDictionary(
                id => id,
                _ => StoryRingStateEnum.None);

            var stats = await _storyViewRepository.GetStoryRingStatsByAuthorAsync(
                currentId,
                normalizedAuthorIds,
                DateTime.UtcNow);

            foreach (var stat in stats)
            {
                if (!result.ContainsKey(stat.AccountId))
                {
                    continue;
                }

                if (stat.VisibleCount <= 0)
                {
                    result[stat.AccountId] = StoryRingStateEnum.None;
                    continue;
                }

                result[stat.AccountId] =
                    stat.UnseenCount > 0
                        ? StoryRingStateEnum.Unseen
                        : StoryRingStateEnum.Seen;
            }

            return result;
        }

        public async Task<StoryActiveItemResponse> ReactStoryAsync(Guid currentId, Guid storyId, StoryReactRequest request)
        {
            var nowUtc = DateTime.UtcNow;
            var story = await _storyRepository.GetStoryByIdAsync(storyId);
            if (story == null || story.IsDeleted)
            {
                throw new NotFoundException("Story not found or expired.");
            }

            if (story.ExpiresAt <= nowUtc)
            {
                throw new BadRequestException("Story has expired.");
            }

            var viewableStoryIds = await _storyRepository.GetViewableStoryIdsAsync(currentId, new[] { storyId }, nowUtc);
            if (viewableStoryIds.Count == 0)
            {
                throw new NotFoundException("Story not found or expired.");
            }

            if (!Enum.IsDefined(typeof(ReactEnum), request.ReactType))
            {
                throw new BadRequestException("Invalid reaction type.");
            }

            var reactType = (ReactEnum)request.ReactType;
            var storyView = await _storyViewRepository.GetStoryViewAsync(storyId, currentId);
            var createdInThisRequest = false;

            if (storyView == null)
            {
                var newStoryView = new StoryView
                {
                    StoryId = storyId,
                    ViewerAccountId = currentId,
                    ViewedAt = nowUtc,
                    ReactType = reactType,
                    ReactedAt = nowUtc
                };

                createdInThisRequest = await _storyViewRepository.TryAddStoryViewAsync(newStoryView);
                if (createdInThisRequest)
                {
                    storyView = newStoryView;
                }
                else
                {
                    // Another request may have inserted the same PK concurrently.
                    storyView = await _storyViewRepository.GetStoryViewAsync(storyId, currentId);
                }
            }

            if (storyView == null)
            {
                throw new NotFoundException("Story not found or expired.");
            }

            if (!createdInThisRequest)
            {
                // Logic Toggle React
                if (storyView.ReactType == reactType)
                {
                    // Unreact
                    storyView.ReactType = null;
                    storyView.ReactedAt = null;
                }
                else
                {
                    // Upsert react
                    storyView.ReactType = reactType;
                    storyView.ReactedAt = nowUtc;
                }

                await _storyViewRepository.UpdateStoryViewAsync(storyView);
            }

            await _unitOfWork.CommitAsync();

            var model = new StoryActiveItemModel
            {
                StoryId = story.StoryId,
                AccountId = story.AccountId,
                ContentType = story.ContentType,
                MediaUrl = story.MediaUrl,
                TextContent = story.TextContent,
                BackgroundColorKey = story.BackgroundColorKey,
                FontTextKey = story.FontTextKey,
                FontSizeKey = story.FontSizeKey,
                TextColorKey = story.TextColorKey,
                Privacy = story.Privacy,
                CreatedAt = story.CreatedAt,
                ExpiresAt = story.ExpiresAt,
                IsViewedByCurrentUser = true,
                CurrentUserReactType = storyView.ReactType
            };

            return _mapper.Map<StoryActiveItemResponse>(model);
        }

        public async Task<PagedResponse<StoryViewerBasicResponse>> GetStoryViewersAsync(Guid currentId, Guid storyId, int page, int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = DefaultViewersPageSize;

            var story = await _storyRepository.GetStoryByIdAsync(storyId);
            if (story == null || story.IsDeleted)
            {
                throw new NotFoundException("Story not found.");
            }

            // Security check: Only owner can see the list
            if (story.AccountId != currentId)
            {
                throw new ForbiddenException("You don't have permission to view viewers of this story.");
            }

            var (items, totalItems) = await _storyViewRepository.GetStoryViewersPagedAsync(storyId, page, pageSize);

            return new PagedResponse<StoryViewerBasicResponse>
            {
                Items = _mapper.Map<List<StoryViewerBasicResponse>>(items),
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}
