using CloudM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CloudM.Infrastructure.Helpers
{
    public static class AccountBlockQueryHelper
    {
        public static IQueryable<Guid> CreateHiddenAccountIdsQuery(AppDbContext context, Guid currentId)
        {
            return context.AccountBlocks
                .AsNoTracking()
                .Where(x => x.BlockerId == currentId)
                .Select(x => x.BlockedId)
                .Concat(
                    context.AccountBlocks
                        .AsNoTracking()
                        .Where(x => x.BlockedId == currentId)
                        .Select(x => x.BlockerId))
                .Distinct();
        }

        public static IQueryable<Guid> CreateBlockedByCurrentIdsQuery(AppDbContext context, Guid currentId)
        {
            return context.AccountBlocks
                .AsNoTracking()
                .Where(x => x.BlockerId == currentId)
                .Select(x => x.BlockedId);
        }

        public static IQueryable<Guid> CreateBlockingCurrentIdsQuery(AppDbContext context, Guid currentId)
        {
            return context.AccountBlocks
                .AsNoTracking()
                .Where(x => x.BlockedId == currentId)
                .Select(x => x.BlockerId);
        }
    }
}
