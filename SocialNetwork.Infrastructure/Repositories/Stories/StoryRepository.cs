using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;

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

        public async Task<bool> HasRecentStoryAsync(Guid accountId, SocialNetwork.Domain.Enums.StoryContentTypeEnum contentType, TimeSpan window)
        {
            var cutoff = DateTime.UtcNow.Subtract(window);
            return await _context.Stories
                .AnyAsync(s => s.AccountId == accountId && 
                               s.ContentType == contentType && 
                               s.CreatedAt >= cutoff &&
                               !s.IsDeleted);
        }

        public async Task<bool> ExistsAndActiveAsync(Guid storyId)
        {
            return await _context.Stories
                .AnyAsync(s => s.StoryId == storyId &&
                               s.ExpiresAt > DateTime.UtcNow &&
                               !s.IsDeleted);
        }

        public async Task<HashSet<Guid>> GetActiveStoryIdsAsync(IEnumerable<Guid> storyIds)
        {
            var idList = storyIds.ToList();
            if (!idList.Any()) return new HashSet<Guid>();

            var activeIds = await _context.Stories
                .AsNoTracking()
                .Where(s => idList.Contains(s.StoryId) &&
                            s.ExpiresAt > DateTime.UtcNow &&
                            !s.IsDeleted)
                .Select(s => s.StoryId)
                .ToListAsync();

            return activeIds.ToHashSet();
        }
    }
}
