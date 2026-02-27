using Microsoft.EntityFrameworkCore;
using CloudM.Application.DTOs.PostMediaDTOs;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.Posts
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
                            PostCode = p.PostCode,
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
                            PostCode = p.PostCode,
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
        public Task UpdatePost(Post post)
        {
            _context.Posts.Update(post);
            return Task.CompletedTask;
        }
        public async Task SoftDeletePostAsync(Guid postId)
        {
            var post = await _context.Posts.FindAsync(postId);
            if (post != null)
            {
                post.IsDeleted = true;
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
                .AsNoTracking()
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
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.PostId);

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
        public async Task<List<PostFeedModel>> GetFeedByScoreAsync(
            Guid currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit)
        {
            var now = DateTime.UtcNow;

            var followedIdsQuery = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            var baseQuery = _context.Posts.AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    p.Account.Status == AccountStatusEnum.Active &&
                    p.Medias.Any() &&
                    (
                        p.Privacy == PostPrivacyEnum.Public ||
                        (p.Privacy == PostPrivacyEnum.FollowOnly && followedIdsQuery.Contains(p.AccountId)) ||
                        p.AccountId == currentId
                    )
                );

            if (cursorCreatedAt.HasValue && cursorPostId.HasValue)
            {
                baseQuery = baseQuery.Where(p =>
                    p.CreatedAt < cursorCreatedAt.Value ||
                    (p.CreatedAt == cursorCreatedAt.Value &&
                     p.PostId.CompareTo(cursorPostId.Value) < 0));
            }

            // Pull only a bounded recent candidate window from DB, then compute score in-memory.
            // This keeps query SQL-safe and avoids loading full media payloads before ranking.
            var candidateLimit = Math.Min(Math.Max(limit * 12, 120), 600);

            var candidatePosts = await baseQuery
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.PostId)
                .Take(candidateLimit)
                .Select(p => new
                {
                    p.PostId,
                    p.PostCode,
                    p.Content,
                    p.Privacy,
                    p.FeedAspectRatio,
                    p.CreatedAt,
                    AuthorAccountId = p.Account.AccountId,
                    AuthorUsername = p.Account.Username,
                    AuthorFullName = p.Account.FullName,
                    AuthorAvatarUrl = p.Account.AvatarUrl,
                    AuthorStatus = p.Account.Status,
                    MediaCount = p.Medias.Count(),
                    ReactCount = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active),
                    CommentCount = p.Comments.Count(c => c.ParentCommentId == null && c.Account.Status == AccountStatusEnum.Active),
                    ReplyCount = p.Comments.Count(c => c.ParentCommentId != null && c.Account.Status == AccountStatusEnum.Active),
                    IsReactedByCurrentUser = p.Reacts.Any(r => r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active),
                    IsOwner = p.AccountId == currentId,
                    IsFollowedAuthor = followedIdsQuery.Contains(p.AccountId)
                })
                .ToListAsync();

            if (candidatePosts.Count == 0)
            {
                return new List<PostFeedModel>();
            }

            var candidateAuthorIds = candidatePosts
                .Select(x => x.AuthorAccountId)
                .Where(id => id != Guid.Empty && id != currentId)
                .Distinct()
                .ToList();

            var authorAffinity = candidateAuthorIds.Count == 0
                ? new Dictionary<Guid, double>()
                : await (
                    _context.PostReacts
                        .AsNoTracking()
                        .Where(r => r.AccountId == currentId && candidateAuthorIds.Contains(r.Post.AccountId))
                        .Select(r => new
                        {
                            AuthorId = r.Post.AccountId,
                            InteractionAt = r.CreatedAt
                        })
                        .Concat(
                            _context.Comments
                                .AsNoTracking()
                                .Where(c => c.AccountId == currentId && candidateAuthorIds.Contains(c.Post.AccountId))
                                .Select(c => new
                                {
                                    AuthorId = c.Post.AccountId,
                                    InteractionAt = c.CreatedAt
                                })
                        )
                        .GroupBy(x => x.AuthorId)
                        .Select(g => new
                        {
                            AuthorId = g.Key,
                            LastInteractionAt = g.Max(x => x.InteractionAt)
                        })
                )
                .ToDictionaryAsync(
                    x => x.AuthorId,
                    x => (now - x.LastInteractionAt).TotalDays
                );

            var topScoredPosts = candidatePosts
                .Select(x =>
                {
                    var freshnessHours = (now - x.CreatedAt).TotalHours;
                    var freshnessScore = freshnessHours < 1 ? 50 : 50 / freshnessHours;
                    var affinityScore = authorAffinity.TryGetValue(x.AuthorAccountId, out var days)
                        ? 40 / (1 + days)
                        : 0;

                    var score =
                        (x.IsFollowedAuthor ? 100 : 0)
                        + affinityScore
                        + x.ReactCount * 2
                        + x.CommentCount * 3
                        + x.ReplyCount
                        + freshnessScore;

                    return new
                    {
                        x.PostId,
                        x.PostCode,
                        x.Content,
                        x.Privacy,
                        x.FeedAspectRatio,
                        x.CreatedAt,
                        x.AuthorAccountId,
                        x.AuthorUsername,
                        x.AuthorFullName,
                        x.AuthorAvatarUrl,
                        x.AuthorStatus,
                        x.MediaCount,
                        x.ReactCount,
                        x.CommentCount,
                        x.ReplyCount,
                        x.IsReactedByCurrentUser,
                        x.IsOwner,
                        x.IsFollowedAuthor,
                        Score = score
                    };
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.PostId)
                .Take(limit)
                .ToList();

            var topPostIds = topScoredPosts
                .Select(x => x.PostId)
                .ToList();

            var medias = await _context.PostMedias
                .AsNoTracking()
                .Where(m => topPostIds.Contains(m.PostId))
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.PostId,
                    Media = new MediaPostPersonalListModel
                    {
                        MediaId = m.MediaId,
                        MediaUrl = m.MediaUrl,
                        Type = m.Type
                    }
                })
                .ToListAsync();

            var mediaLookup = medias
                .GroupBy(x => x.PostId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Media).ToList());

            return topScoredPosts
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
                        AccountId = x.AuthorAccountId,
                        Username = x.AuthorUsername,
                        FullName = x.AuthorFullName,
                        AvatarUrl = x.AuthorAvatarUrl,
                        Status = x.AuthorStatus,
                        IsFollowedByCurrentUser = x.IsOwner || x.IsFollowedAuthor
                    },
                    Medias = mediaLookup.TryGetValue(x.PostId, out var postMedias)
                        ? postMedias
                        : new List<MediaPostPersonalListModel>(),
                    MediaCount = x.MediaCount,
                    ReactCount = x.ReactCount,
                    CommentCount = x.CommentCount,
                    ReplyCount = x.ReplyCount,
                    IsFollowedAuthor = x.IsFollowedAuthor,
                    IsReactedByCurrentUser = x.IsReactedByCurrentUser,
                    IsOwner = x.IsOwner
                })
                .ToList();
        }


    }
}
