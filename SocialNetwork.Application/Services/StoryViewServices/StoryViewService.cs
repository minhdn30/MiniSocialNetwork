using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.StoryDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.StoryViews;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.StoryViewServices
{
    public class StoryViewService : IStoryViewService
    {
        private const int DefaultAuthorsPageSize = 20;
        private const int MaxAuthorsPageSize = 50;
        private const int DefaultTopViewersCount = 3;

        private readonly IStoryViewRepository _storyViewRepository;
        private readonly IUnitOfWork _unitOfWork;

        public StoryViewService(
            IStoryViewRepository storyViewRepository,
            IUnitOfWork unitOfWork)
        {
            _storyViewRepository = storyViewRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResponse<StoryAuthorItemResponse>> GetViewableAuthorsAsync(Guid currentId, int page, int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = DefaultAuthorsPageSize;
            if (pageSize > MaxAuthorsPageSize) pageSize = MaxAuthorsPageSize;

            var (items, totalItems) = await _storyViewRepository.GetViewableAuthorSummariesAsync(
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
                    : (item.UnseenCount > 0 ? StoryRingStateEnum.Unseen : StoryRingStateEnum.Seen)
            }).ToList();

            return new PagedResponse<StoryAuthorItemResponse>(responses, page, pageSize, totalItems);
        }

        public async Task<StoryAuthorActiveResponse> GetActiveStoriesByAuthorAsync(Guid currentId, Guid authorId)
        {
            if (authorId == Guid.Empty)
            {
                throw new BadRequestException("AuthorId is required.");
            }

            var storyItems = await _storyViewRepository.GetActiveStoriesByAuthorAsync(currentId, authorId, DateTime.UtcNow);
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
                IsViewedByCurrentUser = item.IsViewedByCurrentUser
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

        public async Task<StoryMarkViewedResponse> MarkStoriesViewedAsync(Guid currentId, StoryMarkViewedRequest request)
        {
            if (request?.StoryIds == null || request.StoryIds.Count == 0)
            {
                throw new BadRequestException("StoryIds is required.");
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
            var visibleStoryIds = await _storyViewRepository.GetViewableStoryIdsAsync(currentId, normalizedStoryIds, nowUtc);
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

            if (toInsert.Count > 0)
            {
                await _storyViewRepository.AddStoryViewsAsync(toInsert);
                await _unitOfWork.CommitAsync();
            }

            return new StoryMarkViewedResponse
            {
                RequestedCount = normalizedStoryIds.Count,
                VisibleCount = visibleStoryIds.Count,
                MarkedCount = toInsert.Count
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
    }
}
