using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.PostSaves
{
    public class PostSaveRepository : IPostSaveRepository
    {
        private readonly AppDbContext _context;

        public PostSaveRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsPostSavedByCurrentAsync(Guid currentId, Guid postId)
        {
            return await _context.PostSaves
                .AsNoTracking()
                .AnyAsync(s => s.AccountId == currentId && s.PostId == postId);
        }

        public async Task<bool> TryAddPostSaveAsync(Guid currentId, Guid postId, DateTime createdAt)
        {
            var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""PostSaves"" (""PostId"", ""AccountId"", ""CreatedAt"")
                VALUES ({postId}, {currentId}, {createdAt})
                ON CONFLICT (""PostId"", ""AccountId"") DO NOTHING");

            return affectedRows > 0;
        }

        public async Task RemovePostSaveAsync(Guid currentId, Guid postId)
        {
            await _context.PostSaves
                .Where(s => s.AccountId == currentId && s.PostId == postId)
                .ExecuteDeleteAsync();
        }

        public async Task<List<PostPersonalListModel>> GetSavedPostsByCurrentCursorAsync(
            Guid currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit)
        {
            var followedIdsQuery = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            IQueryable<PostSave> query = _context.PostSaves
                .AsNoTracking()
                .Where(s =>
                    s.AccountId == currentId &&
                    !s.Post.IsDeleted &&
                    s.Post.Account.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(s.Post.Account.RoleId) &&
                    s.Post.Medias.Any() &&
                    (
                        s.Post.AccountId == currentId ||
                        s.Post.Privacy == PostPrivacyEnum.Public ||
                        (s.Post.Privacy == PostPrivacyEnum.FollowOnly &&
                         followedIdsQuery.Contains(s.Post.AccountId))
                    ));

            if (cursorCreatedAt.HasValue && cursorPostId.HasValue)
            {
                query = query.Where(s =>
                    s.CreatedAt < cursorCreatedAt.Value ||
                    (s.CreatedAt == cursorCreatedAt.Value &&
                     s.PostId.CompareTo(cursorPostId.Value) < 0));
            }

            var posts = await query
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.PostId)
                .Take(limit)
                .Select(s => new PostPersonalListModel
                {
                    PostId = s.Post.PostId,
                    PostCode = s.Post.PostCode,
                    Medias = s.Post.Medias
                        .OrderBy(m => m.CreatedAt)
                        .Select(m => new MediaPostPersonalListModel
                        {
                            MediaId = m.MediaId,
                            MediaUrl = m.MediaUrl,
                            Type = m.Type
                        })
                        .Take(1)
                        .ToList(),
                    MediaCount = s.Post.Medias.Count(),
                    ReactCount = s.Post.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    CommentCount = s.Post.Comments.Count(c => c.ParentCommentId == null && c.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId)),
                    SavedAt = s.CreatedAt
                })
                .ToListAsync();

            return posts;
        }
    }
}
