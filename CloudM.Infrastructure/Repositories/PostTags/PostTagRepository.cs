using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.PostTags
{
    public class PostTagRepository : IPostTagRepository
    {
        private readonly AppDbContext _context;

        public PostTagRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> RemoveCurrentUserTagAsync(Guid postId, Guid currentId)
        {
            if (postId == Guid.Empty || currentId == Guid.Empty)
            {
                return false;
            }

            var targetTag = await _context.PostTags
                .FirstOrDefaultAsync(x =>
                    x.PostId == postId &&
                    x.TaggedAccountId == currentId &&
                    !x.Post.IsDeleted &&
                    x.Post.Account.Status == AccountStatusEnum.Active);

            if (targetTag == null)
            {
                return false;
            }

            _context.PostTags.Remove(targetTag);
            return true;
        }
    }
}
