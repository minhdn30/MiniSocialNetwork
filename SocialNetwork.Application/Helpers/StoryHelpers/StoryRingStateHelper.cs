using SocialNetwork.Application.Services.StoryServices;
using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Helpers.StoryHelpers
{
    public class StoryRingStateHelper : IStoryRingStateHelper
    {
        private readonly IStoryService? _storyService;
        private readonly Dictionary<(Guid CurrentId, Guid TargetId), StoryRingStateEnum> _cache = new();

        public StoryRingStateHelper(IStoryService? storyService = null)
        {
            _storyService = storyService;
        }

        public async Task<StoryRingStateEnum> ResolveAsync(Guid? currentId, Guid targetId)
        {
            if (!currentId.HasValue || targetId == Guid.Empty || _storyService == null)
            {
                return StoryRingStateEnum.None;
            }

            var cacheKey = (currentId.Value, targetId);
            if (_cache.TryGetValue(cacheKey, out var cachedState))
            {
                return cachedState;
            }

            var stateMap = await _storyService.GetStoryRingStatesForAuthorsAsync(currentId.Value, new[] { targetId });
            var resolvedState = stateMap.TryGetValue(targetId, out var ringState)
                ? ringState
                : StoryRingStateEnum.None;

            _cache[cacheKey] = resolvedState;
            return resolvedState;
        }

        public async Task<IReadOnlyDictionary<Guid, StoryRingStateEnum>> ResolveManyAsync(Guid currentId, IEnumerable<Guid> targetIds)
        {
            if (currentId == Guid.Empty || targetIds == null)
            {
                return new Dictionary<Guid, StoryRingStateEnum>();
            }

            var normalizedTargetIds = targetIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedTargetIds.Count == 0)
            {
                return new Dictionary<Guid, StoryRingStateEnum>();
            }

            if (_storyService == null)
            {
                return normalizedTargetIds.ToDictionary(id => id, _ => StoryRingStateEnum.None);
            }

            var missingIds = normalizedTargetIds
                .Where(id => !_cache.ContainsKey((currentId, id)))
                .ToList();

            if (missingIds.Count > 0)
            {
                var stateMap = await _storyService.GetStoryRingStatesForAuthorsAsync(currentId, missingIds);
                foreach (var targetId in missingIds)
                {
                    _cache[(currentId, targetId)] = stateMap.TryGetValue(targetId, out var ringState)
                        ? ringState
                        : StoryRingStateEnum.None;
                }
            }

            return normalizedTargetIds.ToDictionary(id => id, id => _cache[(currentId, id)]);
        }
    }
}
