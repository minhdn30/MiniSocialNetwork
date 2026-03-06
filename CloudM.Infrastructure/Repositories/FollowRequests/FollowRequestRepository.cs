using CloudM.Domain.Entities;
using CloudM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CloudM.Infrastructure.Repositories.FollowRequests
{
    public class FollowRequestRepository : IFollowRequestRepository
    {
        private readonly AppDbContext _context;

        public FollowRequestRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsFollowRequestExistAsync(Guid requesterId, Guid targetId)
        {
            return await _context.FollowRequests
                .AnyAsync(fr => fr.RequesterId == requesterId && fr.TargetId == targetId);
        }

        public Task AddFollowRequestAsync(FollowRequest followRequest)
        {
            _context.FollowRequests.Add(followRequest);
            return Task.CompletedTask;
        }

        public async Task RemoveFollowRequestAsync(Guid requesterId, Guid targetId)
        {
            await _context.FollowRequests
                .Where(fr => fr.RequesterId == requesterId && fr.TargetId == targetId)
                .ExecuteDeleteAsync();
        }
    }
}
