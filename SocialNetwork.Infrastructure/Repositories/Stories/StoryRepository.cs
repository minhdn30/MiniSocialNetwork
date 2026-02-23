using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace SocialNetwork.Infrastructure.Repositories.Stories
{
    public class StoryRepository : IStoryRepository
    {
        private readonly AppDbContext _context;

        public StoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddStoryAsync(Story story)
        {
            await _context.Stories.AddAsync(story);
        }

        public async Task<Story?> GetStoryByIdAsync(Guid storyId)
        {
            return await _context.Stories.FirstOrDefaultAsync(s => s.StoryId == storyId);
        }

        public Task UpdateStoryAsync(Story story)
        {
            _context.Stories.Update(story);
            return Task.CompletedTask;
        }

        public async Task<List<StoryRingStatsByAuthorModel>> GetStoryRingStatsByAuthorAsync(
            Guid currentId,
            IReadOnlyCollection<Guid> authorIds,
            DateTime nowUtc)
        {
            if (authorIds == null || authorIds.Count == 0)
            {
                return new List<StoryRingStatsByAuthorModel>();
            }

            var normalizedAuthorIds = authorIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedAuthorIds.Count == 0)
            {
                return new List<StoryRingStatsByAuthorModel>();
            }

            var followedIds = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            var viewedStoryIds = _context.StoryViews
                .AsNoTracking()
                .Where(v => v.ViewerAccountId == currentId)
                .Select(v => v.StoryId);

            return await _context.Stories
                .AsNoTracking()
                .Where(s =>
                    normalizedAuthorIds.Contains(s.AccountId) &&
                    !s.IsDeleted &&
                    s.ExpiresAt > nowUtc &&
                    s.Account.Status == AccountStatusEnum.Active &&
                    (
                        s.AccountId == currentId ||
                        s.Privacy == StoryPrivacyEnum.Public ||
                        (s.Privacy == StoryPrivacyEnum.FollowOnly &&
                         followedIds.Contains(s.AccountId))
                    ))
                .Select(s => new
                {
                    s.AccountId,
                    IsViewed = viewedStoryIds.Contains(s.StoryId)
                })
                .GroupBy(x => x.AccountId)
                .Select(g => new StoryRingStatsByAuthorModel
                {
                    AccountId = g.Key,
                    VisibleCount = g.Count(),
                    UnseenCount = g.Count(x => !x.IsViewed)
                })
                .ToListAsync();
        }
    }
}
