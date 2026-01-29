using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Posts
{
    public class PostRepository : IPostRepository
    {
        private readonly AppDbContext _context;
        public PostRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<Post?> GetPostById(Guid postId)
        {
            return await _context.Posts
                .Include(p => p.Account)
                .Include(p => p.Medias)
                .Include(p => p.Reacts)
                .Include(p => p.Comments)
                .FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted);
        }
        public async Task AddPost(Post post)
        {
            await _context.Posts.AddAsync(post);
            await _context.SaveChangesAsync();
        }
        public async Task UpdatePost(Post post)
        {
            _context.Posts.Update(post);
            await _context.SaveChangesAsync();
        }
        public async Task SoftDeletePostAsync(Guid postId)
        {
            var post = await _context.Posts.FindAsync(postId);
            if (post != null)
            {
                post.IsDeleted = true;
                await _context.SaveChangesAsync();
            }
        }
        public async Task<(IEnumerable<PostPersonalListModel> posts, int TotalItems)> GetPostsByAccountId(Guid accountId, Guid? currentId, int page, int pageSize)
        {
            bool isOwner = currentId == accountId;
            bool isFollower = false;

            if (currentId.HasValue && !isOwner)
            {
                isFollower = await _context.Follows.AnyAsync(f =>
                    f.FollowerId == currentId &&
                    f.FollowedId == accountId);
            }

            var query = _context.Posts
                .Where(p =>
                    p.AccountId == accountId &&
                    !p.IsDeleted &&
                    (
                        isOwner ||
                        p.Privacy == PostPrivacyEnum.Public ||
                        (p.Privacy == PostPrivacyEnum.FollowOnly && isFollower)
                    )
                )
                .OrderByDescending(p => p.CreatedAt);

            var totalItems = await query.CountAsync();

            var posts = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PostPersonalListModel
                {
                    PostId = p.PostId,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    Medias = p.Medias
                        .Select(m => new MediaPostPersonalListModel
                        {
                            MediaId = m.MediaId,
                            MediaUrl = m.MediaUrl,
                            Type = m.Type
                        })
                        .ToList(),
                    MediaCount = p.Medias.Count(),
                    ReactCount = p.Reacts.Count(),
                    CommentCount = p.Comments.Count(),
                    IsReactedByCurrentUser =
                        currentId.HasValue &&
                        p.Reacts.Any(r => r.AccountId == currentId)
                })
                .ToListAsync();

            return (posts, totalItems);
        }

        public async Task<int> CountPostsByAccountIdAsync(Guid accountId)
        {
            return await _context.Posts
                .Where(p => p.AccountId == accountId && !p.IsDeleted)
                .CountAsync();
        }
        public async Task<bool> IsPostExist(Guid postId)
        {
            return await _context.Posts.AnyAsync(p => p.PostId == postId && !p.IsDeleted);
        }
        public async Task<List<PostFeedModel>> GetFeedByTimelineAsync(Guid currentId, DateTime? cursorCreatedAt, Guid? cursorPostId, int limit)
        {
            var query = _context.Posts.AsNoTracking()
                        .Where(p => !p.IsDeleted && ( p.Privacy == PostPrivacyEnum.Public
                        || (p.Privacy == PostPrivacyEnum.FollowOnly && _context.Follows.Any(f =>
                            f.FollowerId == currentId && f.FollowedId == p.AccountId))
                        || p.AccountId == currentId));

            // Cursor-based pagination
            if (cursorCreatedAt.HasValue && cursorPostId.HasValue)
            {
                query = query.Where(p => p.CreatedAt < cursorCreatedAt.Value
                    || (p.CreatedAt == cursorCreatedAt.Value && p.PostId.CompareTo(cursorPostId.Value) < 0));
            }
            return await query.OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.PostId)
                .Take(limit)
                .Select(p => new PostFeedModel
                {
                   PostId = p.PostId,
                   Content = p.Content,
                   Privacy = p.Privacy,
                   CreatedAt = p.CreatedAt,
                   Author = new AccountBasicInfoModel
                   {
                       AccountId = p.Account.AccountId,
                       Username = p.Account.Username,
                       FullName = p.Account.FullName,
                       AvatarUrl = p.Account.AvatarUrl
                   },
                   Medias = p.Medias.OrderBy(m => m.CreatedAt)
                   .Take(3) //preview media
                   .Select(m => new MediaPostPersonalListModel
                   {
                       MediaId = m.MediaId,
                       MediaUrl = m.MediaUrl,
                       Type = m.Type
                   }).ToList(),
                   MediaCount = p.Medias.Count(),
                   ReactCount = p.Reacts.Count(),
                   CommentCount = p.Comments.Count(),
                   IsReactedByCurrentUser = p.Reacts.Any(r => r.AccountId == currentId),
                   IsOwner = p.AccountId == currentId
                }).ToListAsync();
        }
        public async Task<List<PostFeedModel>> GetFeedByScoreAsync(Guid currentId, DateTime? cursorCreatedAt, Guid? cursorPostId, int limit)
        {
            var now = DateTime.UtcNow;

            var followedIds = _context.Follows
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            // Base query (Privacy)
            var baseQuery = _context.Posts.AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    (
                        p.Privacy == PostPrivacyEnum.Public ||
                        (p.Privacy == PostPrivacyEnum.FollowOnly && followedIds.Contains(p.AccountId)) ||
                        p.AccountId == currentId
                    )
                );

            // 2Cursor pagination
            if (cursorCreatedAt.HasValue && cursorPostId.HasValue)
            {
                baseQuery = baseQuery.Where(p =>
                    p.CreatedAt < cursorCreatedAt.Value ||
                    (p.CreatedAt == cursorCreatedAt.Value && p.PostId.CompareTo(cursorPostId.Value) < 0));
            }

            // Get author affinity (interaction decay)
            var authorAffinity = await (
                from p in _context.Posts
                where
                    p.AccountId != currentId &&
                    (
                        p.Reacts.Any(r => r.AccountId == currentId) ||
                        p.Comments.Any(c => c.AccountId == currentId)
                    )
                select new
                {
                    AuthorId = p.AccountId,
                    LastInteractionAt =
                        p.Reacts
                            .Where(r => r.AccountId == currentId)
                            .Select(r => r.CreatedAt)
                            .Concat(
                                p.Comments
                                    .Where(c => c.AccountId == currentId)
                                    .Select(c => c.CreatedAt)
                            )
                            .Max()
                }
            )
            .GroupBy(x => x.AuthorId)
            .Select(g => new
            {
                AuthorId = g.Key,
                LastInteractionAt = g.Max(x => x.LastInteractionAt)
            })
            .ToDictionaryAsync(
                x => x.AuthorId,
                x => (now - x.LastInteractionAt).TotalDays
            );

            //Query + score
            var queryWithScore = baseQuery
                .Select(p => new
                {
                    Post = p,
                    ReactCount = p.Reacts.Count(),
                    CommentCount = p.Comments.Count(),
                    IsFollowedAuthor = followedIds.Contains(p.AccountId),
                    FreshnessHours = (now - p.CreatedAt).TotalHours,
                    HasInteractedAuthor = authorAffinity.ContainsKey(p.AccountId),
                    InteractionDays =
                        authorAffinity.ContainsKey(p.AccountId)
                            ? authorAffinity[p.AccountId]
                            : (double?)null
                })
                .AsEnumerable() // Switch to memory for complex score calculations
                .Select(x => new
                {
                    x.Post,
                    x.ReactCount,
                    x.CommentCount,
                    Score =
                        (x.IsFollowedAuthor ? 100 : 0)
                      + (x.HasInteractedAuthor
                            ? 40 / (1 + x.InteractionDays!.Value)
                            : 0)
                      + x.ReactCount * 2
                      + x.CommentCount * 3
                      + (x.FreshnessHours < 1 ? 50 : 50 / x.FreshnessHours)
                });

            // Order + map result
            return queryWithScore
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Post.CreatedAt)
                .ThenByDescending(x => x.Post.PostId)
                .Take(limit)
                .Select(x => new PostFeedModel
                {
                    PostId = x.Post.PostId,
                    Content = x.Post.Content,
                    Privacy = x.Post.Privacy,
                    CreatedAt = x.Post.CreatedAt,
                    Author = new AccountBasicInfoModel
                    {
                        AccountId = x.Post.Account.AccountId,
                        Username = x.Post.Account.Username,
                        FullName = x.Post.Account.FullName,
                        AvatarUrl = x.Post.Account.AvatarUrl
                    },
                    Medias = x.Post.Medias
                        .OrderBy(m => m.CreatedAt)
                        .Take(3)
                        .Select(m => new MediaPostPersonalListModel
                        {
                            MediaId = m.MediaId,
                            MediaUrl = m.MediaUrl,
                            Type = m.Type
                        })
                        .ToList(),
                    MediaCount = x.Post.Medias.Count(),
                    ReactCount = x.ReactCount,
                    CommentCount = x.CommentCount,
                    IsReactedByCurrentUser = x.Post.Reacts.Any(r => r.AccountId == currentId),
                    IsOwner = x.Post.AccountId == currentId
                })
                .ToList();
        }

    }
}
