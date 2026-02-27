using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CloudM.Application.Helpers.StoryHelpers
{
    public interface IStoryRingStateHelper
    {
        Task<StoryRingStateEnum> ResolveAsync(Guid? currentId, Guid targetId);
        Task<IReadOnlyDictionary<Guid, StoryRingStateEnum>> ResolveManyAsync(Guid currentId, IEnumerable<Guid> targetIds);
    }
}
