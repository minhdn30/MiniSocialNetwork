using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;
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
    }
}
