using Microsoft.EntityFrameworkCore;
using SocialNetwork.Application.DTOs.PostMediaDTOs;
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
                .FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted && p.Account.Status == AccountStatusEnum.Active);
        }
        public async Task<Post?> GetPostBasicInfoById(Guid postId)
        {
            return await _context.Posts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted && p.Account.Status == AccountStatusEnum.Active);
        }
        public async Task<Post?> GetPostForUpdateContent(Guid postId)
        {
             return await _context.Posts
                .Include(p => p.Medias)
                .FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted && p.Account.Status == AccountStatusEnum.Active);
        }
        public async Task<PostDetailModel?> GetPostDetailByPostId(Guid postId, Guid currentId)
        {
            var isFollower = await _context.Follows.AnyAsync(f =>
                f.FollowerId == currentId &&
                _context.Posts.Any(p => p.PostId == postId && p.AccountId == f.FollowedId)
            );

            var post = await _context.Posts
                .AsNoTracking()
                .Where(p =>
                    p.PostId == postId &&
                    !p.IsDeleted &&
                    p.Account.Status == AccountStatusEnum.Active &&
                    (
                        p.AccountId == currentId || // owner
                        p.Privacy == PostPrivacyEnum.Public ||
                        (p.Privacy == PostPrivacyEnum.FollowOnly && isFollower)
                    )
                )
                .Select(p => new PostDetailModel
                {
                    PostId = p.PostId,
                    PostCode = p.PostCode,
                    Privacy = (int)p.Privacy,
                    FeedAspectRatio = (int)p.FeedAspectRatio,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,

                    Owner = new AccountBasicInfoModel
                    {
                        AccountId = p.Account.AccountId,
                        Username = p.Account.Username,
                        FullName = p.Account.FullName,
                        AvatarUrl = p.Account.AvatarUrl,
                        Status = p.Account.Status
                    },

                    Medias = p.Medias
                        .OrderBy(m => m.CreatedAt)
                        .Select(m => new PostMediaProfilePreviewModel
                        {
                            MediaId = m.MediaId,
                            PostId = m.PostId,
                            MediaUrl = m.MediaUrl,
                            MediaType = m.Type
                        })
                        .ToList(),

                    TotalMedias = p.Medias.Count(),
                    TotalReacts = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active),
                    TotalComments = p.Comments.Count(c => c.ParentCommentId == null && c.Account.Status == AccountStatusEnum.Active),

                    IsReactedByCurrentUser = p.Reacts.Any(r => r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active),
                    IsOwner = p.AccountId == currentId,
                    IsFollowedByCurrentUser = isFollower
                })
                .FirstOrDefaultAsync();

            return post;
        }

        public async Task<PostDetailModel?> GetPostDetailByPostCode(string postCode, Guid currentId)
        {
            var postRecord = await _context.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.PostCode == postCode);
            if (postRecord == null) return null;

            var isFollower = await _context.Follows.AnyAsync(f =>
                f.FollowerId == currentId &&
                f.FollowedId == postRecord.AccountId
            );

            var post = await _context.Posts
                .AsNoTracking()
                .Where(p =>
                    p.PostCode == postCode &&
                    !p.IsDeleted &&
                    p.Account.Status == AccountStatusEnum.Active &&
                    (
                        p.AccountId == currentId || // owner
                        p.Privacy == PostPrivacyEnum.Public ||
                        (p.Privacy == PostPrivacyEnum.FollowOnly && isFollower)
                    )
                )
                .Select(p => new PostDetailModel
                {
                    PostId = p.PostId,
                    PostCode = p.PostCode,
                    Privacy = (int)p.Privacy,
                    FeedAspectRatio = (int)p.FeedAspectRatio,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,

                    Owner = new AccountBasicInfoModel
                    {
                        AccountId = p.Account.AccountId,
                        Username = p.Account.Username,
                        FullName = p.Account.FullName,
                        AvatarUrl = p.Account.AvatarUrl,
                        Status = p.Account.Status
                    },

                    Medias = p.Medias
                        .OrderBy(m => m.CreatedAt)
                        .Select(m => new PostMediaProfilePreviewModel
                        {
                            MediaId = m.MediaId,
                            PostId = m.PostId,
                            MediaUrl = m.MediaUrl,
                            MediaType = m.Type
                        })
                        .ToList(),

                    TotalMedias = p.Medias.Count(),
                    TotalReacts = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active),
                    TotalComments = p.Comments.Count(c => c.ParentCommentId == null && c.Account.Status == AccountStatusEnum.Active),

                    IsReactedByCurrentUser = p.Reacts.Any(r => r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active),
                    IsOwner = p.AccountId == currentId,
                    IsFollowedByCurrentUser = isFollower
                })
                .FirstOrDefaultAsync();

            return post;
        }

        public async Task AddPost(Post post)
        {
            await _context.Posts.AddAsync(post);
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
                    p.Account.Status == AccountStatusEnum.Active &&
                    p.Medias.Any() &&
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
                    PostCode = p.PostCode,
                    Medias = p.Medias
                        .OrderBy(m => m.CreatedAt)
                        .Select(m => new MediaPostPersonalListModel
                        {
                            MediaId = m.MediaId,
                            MediaUrl = m.MediaUrl,
                            Type = m.Type
                        })
                        .Take(1)
                        .ToList(),
                    MediaCount = p.Medias.Count(),
                    ReactCount = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active),
                    CommentCount = p.Comments.Count(c => c.ParentCommentId == null && c.Account.Status == AccountStatusEnum.Active)
                })
                .ToListAsync();

            return (posts, totalItems);
        }

        public async Task<int> CountPostsByAccountIdAsync(Guid accountId)
        {
            return await _context.Posts
                .Where(p => p.AccountId == accountId && !p.IsDeleted && p.Account.Status == AccountStatusEnum.Active)
                .CountAsync();
        }
        public async Task<bool> IsPostExist(Guid postId)
        {
            return await _context.Posts.AnyAsync(p => p.PostId == postId && !p.IsDeleted && p.Account.Status == AccountStatusEnum.Active);
        }

        public async Task<bool> IsPostCodeExist(string postCode)
        {
            return await _context.Posts.AnyAsync(p => p.PostCode == postCode);
        }
        //no use
        public async Task<List<PostFeedModel>> GetFeedByTimelineAsync(Guid currentId, DateTime? cursorCreatedAt, Guid? cursorPostId, int limit)
        {
            var query = _context.Posts.AsNoTracking()
                        .Where(p => !p.IsDeleted && 
                               p.Account.Status == AccountStatusEnum.Active &&
                               p.Medias.Any() &&
                               ( p.Privacy == PostPrivacyEnum.Public
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
                   PostCode = p.PostCode,
                   Content = p.Content,
                   Privacy = p.Privacy,
                   FeedAspectRatio = p.FeedAspectRatio,
                    CreatedAt = p.CreatedAt,
                   Author = new AccountOnFeedModel
                   {
                       AccountId = p.Account.AccountId,
                       Username = p.Account.Username,
                       FullName = p.Account.FullName,
                       AvatarUrl = p.Account.AvatarUrl,
                       Status = p.Account.Status,
                       IsFollowedByCurrentUser = p.AccountId == currentId || _context.Follows.Any(f =>
                           f.FollowerId == currentId && f.FollowedId == p.AccountId)
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
                   ReactCount = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active),
                   CommentCount = p.Comments.Count(c => c.ParentCommentId == null && c.Account.Status == AccountStatusEnum.Active),
                   IsReactedByCurrentUser = p.Reacts.Any(r => r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active),
                   IsOwner = p.AccountId == currentId
                }).ToListAsync();
        }
        public async Task<List<PostFeedModel>> GetFeedByScoreAsync(Guid currentId, DateTime? cursorCreatedAt,
            Guid? cursorPostId, int limit)
        {
            var now = DateTime.UtcNow;

            var followedIds = _context.Follows
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            // Base query (privacy)
            var baseQuery = _context.Posts.AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    p.Account.Status == AccountStatusEnum.Active &&
                    p.Medias.Any() &&
                    (
                        p.Privacy == PostPrivacyEnum.Public ||
                        (p.Privacy == PostPrivacyEnum.FollowOnly && followedIds.Contains(p.AccountId)) ||
                        p.AccountId == currentId
                    )
                );

            // Cursor pagination
            if (cursorCreatedAt.HasValue && cursorPostId.HasValue)
            {
                baseQuery = baseQuery.Where(p =>
                    p.CreatedAt < cursorCreatedAt.Value ||
                    (p.CreatedAt == cursorCreatedAt.Value &&
                     p.PostId.CompareTo(cursorPostId.Value) < 0));
            }

            // Author affinity (interaction decay)
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

            // Projection (SQL)
            var projected = baseQuery
                .Select(p => new
                {
                    p.PostId,
                    p.PostCode,
                    p.AccountId,
                    p.Content,
                    p.Privacy,
                    p.FeedAspectRatio,
                    p.CreatedAt,

                    Author = new
                    {
                        p.Account.AccountId,
                        p.Account.Username,
                        p.Account.FullName,
                        p.Account.AvatarUrl,
                        p.Account.Status
                    },

                    Medias = p.Medias
                        .OrderBy(m => m.CreatedAt)
                        .Take(3)
                        .Select(m => new
                        {
                            m.MediaId,
                            m.MediaUrl,
                            m.Type
                        }),

                    MediaCount = p.Medias.Count(),
                    ReactCount = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active),

                    CommentCount = p.Comments.Count(c => c.ParentCommentId == null && c.Account.Status == AccountStatusEnum.Active),
                    ReplyCount = p.Comments.Count(c => c.ParentCommentId != null && c.Account.Status == AccountStatusEnum.Active),

                    IsReactedByCurrentUser = p.Reacts.Any(r => r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active),
                    IsOwner = p.AccountId == currentId,
                    IsFollowedAuthor = followedIds.Contains(p.AccountId),

                    FreshnessHours = (now - p.CreatedAt).TotalHours
                })
                .AsEnumerable() // safe: everything needed is already selected
                .Select(x => new
                {
                    x.PostId,
                    x.PostCode,
                    x.Content,
                    x.Privacy,
                    x.FeedAspectRatio,
                    x.CreatedAt,
                    x.Author,

                    Medias = x.Medias.Select(m => new MediaPostPersonalListModel
                    {
                        MediaId = m.MediaId,
                        MediaUrl = m.MediaUrl,
                        Type = m.Type
                    }).ToList(),

                    x.MediaCount,
                    x.ReactCount,
                    x.CommentCount,
                    x.ReplyCount,
                    x.IsReactedByCurrentUser,
                    x.IsOwner,
                    x.IsFollowedAuthor,

                    InteractionDays = authorAffinity.ContainsKey(x.Author.AccountId)
                        ? (double?)authorAffinity[x.Author.AccountId]
                        : null,

                    // SCORE
                    Score =
                        (x.IsFollowedAuthor ? 100 : 0)
                      + (authorAffinity.ContainsKey(x.Author.AccountId)
                            ? 40 / (1 + authorAffinity[x.Author.AccountId])
                            : 0)
                      + x.ReactCount * 2
                      + x.CommentCount * 3   // comment
                      + x.ReplyCount * 1     // reply
                      + (x.FreshnessHours < 1 ? 50 : 50 / x.FreshnessHours)
                });

            // Order + map result
            return projected
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.PostId)
                .Take(limit)
                .Select(x => new PostFeedModel
                {
                    PostId = x.PostId,
                    PostCode = x.PostCode,
                    Content = x.Content,
                    Privacy = x.Privacy,
                    FeedAspectRatio = x.FeedAspectRatio,
                    CreatedAt = x.CreatedAt,

                    Author = new AccountOnFeedModel
                    {
                        AccountId = x.Author.AccountId,
                        Username = x.Author.Username,
                        FullName = x.Author.FullName,
                        AvatarUrl = x.Author.AvatarUrl,
                        Status = x.Author.Status,
                        IsFollowedByCurrentUser = x.IsOwner || x.IsFollowedAuthor
                    },

                    Medias = x.Medias,
                    MediaCount = x.MediaCount,
                    ReactCount = x.ReactCount,
                    CommentCount = x.CommentCount,
                    IsReactedByCurrentUser = x.IsReactedByCurrentUser,
                    IsOwner = x.IsOwner
                })
                .ToList();
        }


    }
}
