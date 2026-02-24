using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Helpers.StoryHelpers
{
    public interface IStoryRingStateHelper
    {
        Task<StoryRingStateEnum> ResolveAsync(Guid? currentId, Guid targetId);
        Task<IReadOnlyDictionary<Guid, StoryRingStateEnum>> ResolveManyAsync(Guid currentId, IEnumerable<Guid> targetIds);
    }
}
