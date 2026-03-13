using Microsoft.EntityFrameworkCore;
using CloudM.Application.DTOs.PostMediaDTOs;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Helpers;
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
                .AsSplitQuery()
                .Include(p => p.Account)
                .Include(p => p.Medias)
                .Include(p => p.Tags)
                    .ThenInclude(t => t.TaggedAccount)
                .Include(p => p.Reacts)
                .Include(p => p.Comments)
                .FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted && p.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId));
        }
        public async Task<Post?> GetPostBasicInfoById(Guid postId)
        {
            return await _context.Posts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted && p.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId));
        }
        public async Task<List<PostMedia>> GetPostMediasByPostIdAsync(Guid postId)
        {
            return await _context.PostMedias
                .AsNoTracking()
                .Where(m => m.PostId == postId)
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.MediaId)
                .Select(m => new PostMedia
                {
                    MediaId = m.MediaId,
                    PostId = m.PostId,
                    MediaUrl = m.MediaUrl,
                    Type = m.Type,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();
        }
        public async Task<Post?> GetPostForUpdateContent(Guid postId)
        {
             return await _context.Posts
                .Include(p => p.Medias)
                .FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted && p.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId));
        }
        public async Task<PostDetailModel?> GetPostDetailByPostId(Guid postId, Guid currentId)
        {
            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            var isFollower = await _context.Follows.AnyAsync(f =>
                f.FollowerId == currentId &&
                _context.Posts.Any(p => p.PostId == postId && p.AccountId == f.FollowedId)
            );
            var isFollowRequestPending = await _context.FollowRequests.AnyAsync(fr =>
                fr.RequesterId == currentId &&
                _context.Posts.Any(p => p.PostId == postId && p.AccountId == fr.TargetId)
            );

            var post = await _context.Posts
                .AsNoTracking()
                .Where(p =>
                    p.PostId == postId &&
                    !p.IsDeleted &&
                    p.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(p.AccountId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId) &&
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
                    TotalReacts = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    TotalComments = p.Comments.Count(c =>
                        c.ParentCommentId == null &&
                        c.Account.Status == AccountStatusEnum.Active &&
                        !hiddenAccountIds.Contains(c.AccountId) &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId)),

                    IsReactedByCurrentUser = p.Reacts.Any(r => r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    IsSavedByCurrentUser = _context.PostSaves.Any(s => s.PostId == p.PostId && s.AccountId == currentId),
                    IsOwner = p.AccountId == currentId,
                    IsCurrentUserTagged = p.Tags.Any(t => t.TaggedAccountId == currentId),
                    IsFollowedByCurrentUser = isFollower,
                    IsFollowRequestPendingByCurrentUser = !isFollower && isFollowRequestPending
                })
                .FirstOrDefaultAsync();

            if (post != null)
            {
                var taggedInfo = await GetOrderedTaggedAccountsByPostIdAsync(post.PostId, currentId, 2);
                post.TaggedAccounts = taggedInfo.Items;
                post.TotalTaggedAccounts = taggedInfo.Total;
            }

            return post;
        }

        public async Task<PostDetailModel?> GetPostDetailByPostCode(string postCode, Guid currentId)
        {
            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            var postRecord = await _context.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.PostCode == postCode);
            if (postRecord == null) return null;

            var isFollower = await _context.Follows.AnyAsync(f =>
                f.FollowerId == currentId &&
                f.FollowedId == postRecord.AccountId
            );
            var isFollowRequestPending = await _context.FollowRequests.AnyAsync(fr =>
                fr.RequesterId == currentId &&
                fr.TargetId == postRecord.AccountId
            );

            var post = await _context.Posts
                .AsNoTracking()
                .Where(p =>
                    p.PostCode == postCode &&
                    !p.IsDeleted &&
                    p.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(p.AccountId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId) &&
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
                    TotalReacts = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    TotalComments = p.Comments.Count(c =>
                        c.ParentCommentId == null &&
                        c.Account.Status == AccountStatusEnum.Active &&
                        !hiddenAccountIds.Contains(c.AccountId) &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId)),

                    IsReactedByCurrentUser = p.Reacts.Any(r => r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    IsSavedByCurrentUser = _context.PostSaves.Any(s => s.PostId == p.PostId && s.AccountId == currentId),
                    IsOwner = p.AccountId == currentId,
                    IsCurrentUserTagged = p.Tags.Any(t => t.TaggedAccountId == currentId),
                    IsFollowedByCurrentUser = isFollower,
                    IsFollowRequestPendingByCurrentUser = !isFollower && isFollowRequestPending
                })
                .FirstOrDefaultAsync();

            if (post != null)
            {
                var taggedInfo = await GetOrderedTaggedAccountsByPostIdAsync(post.PostId, currentId, 2);
                post.TaggedAccounts = taggedInfo.Items;
                post.TotalTaggedAccounts = taggedInfo.Total;
            }

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
        public async Task<List<PostPersonalListModel>> GetPostsByAccountIdByCursor(
            Guid accountId,
            Guid? currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit)
        {
            if (limit <= 0) limit = 10;

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
                    SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId) &&
                    p.Medias.Any() &&
                    (
                        isOwner ||
                        p.Privacy == PostPrivacyEnum.Public ||
                        (p.Privacy == PostPrivacyEnum.FollowOnly && isFollower)
                    )
                );

            var hiddenAccountIds = currentId.HasValue && currentId.Value != accountId
                ? AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId.Value)
                : _context.AccountBlocks
                    .Where(_ => false)
                    .Select(b => b.BlockedId);

            if (currentId.HasValue && currentId.Value != accountId)
            {
                query = query.Where(p => !hiddenAccountIds.Contains(p.AccountId));
            }

            if (cursorCreatedAt.HasValue && cursorPostId.HasValue)
            {
                query = query.Where(p =>
                    p.CreatedAt < cursorCreatedAt.Value ||
                    (p.CreatedAt == cursorCreatedAt.Value &&
                     p.PostId.CompareTo(cursorPostId.Value) < 0));
            }

            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.PostId)
                .Take(limit)
                .Select(p => new PostPersonalListModel
                {
                    PostId = p.PostId,
                    PostCode = p.PostCode,
                    CreatedAt = p.CreatedAt,
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
                    ReactCount = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    CommentCount = p.Comments.Count(c =>
                        c.ParentCommentId == null &&
                        c.Account.Status == AccountStatusEnum.Active &&
                        !hiddenAccountIds.Contains(c.AccountId) &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId))
                })
                .ToListAsync();

            return posts;
        }

        public async Task<List<PostPersonalListModel>> GetTaggedPostsByAccountIdByCursor(
            Guid accountId,
            Guid currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit)
        {
            if (limit <= 0) limit = 10;

            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            var followedIdsQuery = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            var query = _context.Posts
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    p.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(p.AccountId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId) &&
                    p.Medias.Any() &&
                    p.Tags.Any(t => t.TaggedAccountId == accountId) &&
                    (
                        p.AccountId == currentId ||
                        p.Privacy == PostPrivacyEnum.Public ||
                        (p.Privacy == PostPrivacyEnum.FollowOnly &&
                         followedIdsQuery.Contains(p.AccountId))
                    )
                );

            if (cursorCreatedAt.HasValue && cursorPostId.HasValue)
            {
                query = query.Where(p =>
                    p.CreatedAt < cursorCreatedAt.Value ||
                    (p.CreatedAt == cursorCreatedAt.Value &&
                     p.PostId.CompareTo(cursorPostId.Value) < 0));
            }

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.PostId)
                .Take(limit)
                .Select(p => new PostPersonalListModel
                {
                    PostId = p.PostId,
                    PostCode = p.PostCode,
                    CreatedAt = p.CreatedAt,
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
                    ReactCount = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    CommentCount = p.Comments.Count(c =>
                        c.ParentCommentId == null &&
                        c.Account.Status == AccountStatusEnum.Active &&
                        !hiddenAccountIds.Contains(c.AccountId) &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId))
                })
                .ToListAsync();
        }

        public async Task<int> CountPostsByAccountIdAsync(Guid accountId)
        {
            return await _context.Posts
                .Where(p => p.AccountId == accountId &&
                            !p.IsDeleted &&
                            p.Medias.Any() &&
                            p.Account.Status == AccountStatusEnum.Active &&
                            SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId))
                .CountAsync();
        }
        public async Task<bool> IsPostExist(Guid postId)
        {
            return await _context.Posts.AnyAsync(p => p.PostId == postId && !p.IsDeleted && p.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId));
        }

        public async Task<bool> IsPostCodeExist(string postCode)
        {
            return await _context.Posts.AnyAsync(p => p.PostCode == postCode);
        }

        public async Task<List<Guid>> GetTaggedAccountIdsByPostIdAsync(Guid postId)
        {
            return await _context.PostTags
                .AsNoTracking()
                .Where(x =>
                    x.PostId == postId &&
                    x.TaggedAccount.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(x.TaggedAccount.RoleId))
                .Select(x => x.TaggedAccountId)
                .ToListAsync();
        }

        public async Task AddPostTagsAsync(IEnumerable<PostTag> postTags)
        {
            var tags = (postTags ?? Enumerable.Empty<PostTag>())
                .Where(x => x.PostId != Guid.Empty && x.TaggedAccountId != Guid.Empty)
                .ToList();

            if (tags.Count == 0)
            {
                return;
            }

            await _context.PostTags.AddRangeAsync(tags);
        }

        public async Task RemovePostTagsAsync(Guid postId, IEnumerable<Guid> taggedAccountIds)
        {
            var targetIds = (taggedAccountIds ?? Enumerable.Empty<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();

            if (targetIds.Count == 0)
            {
                return;
            }

            var tagsToRemove = await _context.PostTags
                .Where(x => x.PostId == postId && targetIds.Contains(x.TaggedAccountId))
                .ToListAsync();

            if (tagsToRemove.Count == 0)
            {
                return;
            }

            _context.PostTags.RemoveRange(tagsToRemove);
        }

        public async Task<List<PostTaggedAccountModel>?> GetTaggedAccountsByPostIdAsync(Guid postId, Guid currentId)
        {
            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            var postInfo = await _context.Posts
                .AsNoTracking()
                .Where(p =>
                    p.PostId == postId &&
                    !p.IsDeleted &&
                    p.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(p.AccountId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId))
                .Select(p => new
                {
                    p.AccountId,
                    p.Privacy
                })
                .FirstOrDefaultAsync();

            if (postInfo == null)
            {
                return null;
            }

            var canViewPost = postInfo.AccountId == currentId;
            if (!canViewPost)
            {
                if (postInfo.Privacy == PostPrivacyEnum.Public)
                {
                    canViewPost = true;
                }
                else if (postInfo.Privacy == PostPrivacyEnum.FollowOnly)
                {
                    canViewPost = await _context.Follows.AnyAsync(f =>
                        f.FollowerId == currentId &&
                        f.FollowedId == postInfo.AccountId);
                }
            }

            if (!canViewPost)
            {
                return null;
            }

            var taggedInfo = await GetOrderedTaggedAccountsByPostIdAsync(postId, currentId);
            return taggedInfo.Items;
        }

        private async Task<(List<PostTaggedAccountModel> Items, int Total)> GetOrderedTaggedAccountsByPostIdAsync(
            Guid postId,
            Guid currentId,
            int? take = null)
        {
            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            var taggedAccounts = await _context.PostTags
                .AsNoTracking()
                .Where(t =>
                    t.PostId == postId &&
                    !hiddenAccountIds.Contains(t.TaggedAccountId) &&
                    ((t.TaggedAccount.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(t.TaggedAccount.RoleId)) || t.TaggedAccountId == currentId))
                .Select(t => new PostTaggedAccountRawModel
                {
                    AccountId = t.TaggedAccountId,
                    Username = t.TaggedAccount.Username,
                    FullName = t.TaggedAccount.FullName,
                    AvatarUrl = t.TaggedAccount.AvatarUrl,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            if (taggedAccounts.Count == 0)
            {
                return (new List<PostTaggedAccountModel>(), 0);
            }

            var taggedAccountIds = taggedAccounts
                .Select(x => x.AccountId)
                .Distinct()
                .ToList();

            var followingIdSet = (await _context.Follows
                    .AsNoTracking()
                    .Where(f =>
                        f.FollowerId == currentId &&
                        taggedAccountIds.Contains(f.FollowedId))
                    .Select(f => f.FollowedId)
                    .ToListAsync())
                .ToHashSet();

            var followRequestIdSet = (await _context.FollowRequests
                    .AsNoTracking()
                    .Where(fr =>
                        fr.RequesterId == currentId &&
                        taggedAccountIds.Contains(fr.TargetId))
                    .Select(fr => fr.TargetId)
                    .ToListAsync())
                .ToHashSet();

            var followerIdSet = (await _context.Follows
                    .AsNoTracking()
                    .Where(f =>
                        f.FollowedId == currentId &&
                        taggedAccountIds.Contains(f.FollowerId))
                    .Select(f => f.FollowerId)
                    .ToListAsync())
                .ToHashSet();

            var orderedAccounts = taggedAccounts
                .OrderByDescending(x => x.AccountId == currentId)
                .ThenByDescending(x => followingIdSet.Contains(x.AccountId))
                .ThenByDescending(x => followRequestIdSet.Contains(x.AccountId))
                .ThenByDescending(x => followerIdSet.Contains(x.AccountId))
                .ThenBy(x => x.CreatedAt)
                .Select(x => new PostTaggedAccountModel
                {
                    AccountId = x.AccountId,
                    Username = x.Username,
                    FullName = x.FullName,
                    AvatarUrl = x.AvatarUrl,
                    IsFollowing = followingIdSet.Contains(x.AccountId),
                    IsFollowRequested = followRequestIdSet.Contains(x.AccountId),
                    IsFollower = followerIdSet.Contains(x.AccountId)
                })
                .ToList();

            var total = orderedAccounts.Count;
            var items = take.HasValue && take.Value > 0
                ? orderedAccounts.Take(take.Value).ToList()
                : orderedAccounts;

            return (items, total);
        }

        //no use
        public async Task<List<PostFeedModel>> GetFeedByTimelineAsync(Guid currentId, DateTime? cursorCreatedAt, Guid? cursorPostId, int limit)
        {
            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            var query = _context.Posts.AsNoTracking()
                        .Where(p => !p.IsDeleted && 
                               p.Account.Status == AccountStatusEnum.Active &&
                               !hiddenAccountIds.Contains(p.AccountId) &&
                               SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId) &&
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
                       ,
                       IsFollowRequestPendingByCurrentUser = p.AccountId != currentId && !_context.Follows.Any(f =>
                           f.FollowerId == currentId && f.FollowedId == p.AccountId) && _context.FollowRequests.Any(fr =>
                           fr.RequesterId == currentId && fr.TargetId == p.AccountId)
                   },
                   Medias = p.Medias.OrderBy(m => m.CreatedAt)
                   .Select(m => new MediaPostPersonalListModel
                   {
                       MediaId = m.MediaId,
                       MediaUrl = m.MediaUrl,
                       Type = m.Type
                   }).ToList(),
                   MediaCount = p.Medias.Count(),
                   ReactCount = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                   CommentCount = p.Comments.Count(c =>
                       c.ParentCommentId == null &&
                       c.Account.Status == AccountStatusEnum.Active &&
                       !hiddenAccountIds.Contains(c.AccountId) &&
                       SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId)),
                   IsReactedByCurrentUser = p.Reacts.Any(r => r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                   IsSavedByCurrentUser = _context.PostSaves.Any(s => s.PostId == p.PostId && s.AccountId == currentId),
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
            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);

            var followedIdsQuery = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            var baseQuery = BuildVisibleFeedPostsQuery(currentId, hiddenAccountIds, followedIdsQuery);

            if (cursorCreatedAt.HasValue && cursorPostId.HasValue)
            {
                baseQuery = baseQuery.Where(p =>
                    p.CreatedAt < cursorCreatedAt.Value ||
                    (p.CreatedAt == cursorCreatedAt.Value &&
                     p.PostId.CompareTo(cursorPostId.Value) < 0));
            }

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
                    ReactCount = p.Reacts.Count(r => r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    CommentCount = p.Comments.Count(c =>
                        c.ParentCommentId == null &&
                        c.Account.Status == AccountStatusEnum.Active &&
                        !hiddenAccountIds.Contains(c.AccountId) &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId)),
                    ReplyCount = p.Comments.Count(c =>
                        c.ParentCommentId != null &&
                        c.Account.Status == AccountStatusEnum.Active &&
                        !hiddenAccountIds.Contains(c.AccountId) &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId)),
                    IsReactedByCurrentUser = p.Reacts.Any(r => r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                    IsSavedByCurrentUser = _context.PostSaves.Any(s => s.PostId == p.PostId && s.AccountId == currentId),
                    IsOwner = p.AccountId == currentId,
                    IsFollowedAuthor = followedIdsQuery.Contains(p.AccountId),
                    IsFollowRequestPendingAuthor = _context.FollowRequests.Any(fr =>
                        fr.RequesterId == currentId && fr.TargetId == p.AccountId)
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

                    return new RankedFeedPostRow
                    {
                        PostId = x.PostId,
                        PostCode = x.PostCode,
                        Content = x.Content,
                        Privacy = x.Privacy,
                        FeedAspectRatio = x.FeedAspectRatio,
                        CreatedAt = x.CreatedAt,
                        AuthorAccountId = x.AuthorAccountId,
                        AuthorUsername = x.AuthorUsername,
                        AuthorFullName = x.AuthorFullName,
                        AuthorAvatarUrl = x.AuthorAvatarUrl,
                        AuthorStatus = x.AuthorStatus,
                        MediaCount = x.MediaCount,
                        ReactCount = x.ReactCount,
                        CommentCount = x.CommentCount,
                        ReplyCount = x.ReplyCount,
                        IsReactedByCurrentUser = x.IsReactedByCurrentUser,
                        IsSavedByCurrentUser = x.IsSavedByCurrentUser,
                        IsOwner = x.IsOwner,
                        IsFollowedAuthor = x.IsFollowedAuthor,
                        IsFollowRequestPendingAuthor = x.IsFollowRequestPendingAuthor,
                        RankingScore = 0m,
                        RankingJitterRank = 0,
                        LegacyScore = score
                    };
                })
                .OrderByDescending(x => x.LegacyScore)
                .ThenByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.PostId)
                .Take(limit)
                .ToList();

            return await HydrateFeedPostsAsync(topScoredPosts, currentId, snapshotAt: null);
        }

        public async Task<List<PostFeedModel>> GetFeedPageAsync(
            Guid currentId,
            PostFeedCursorModel cursor,
            FeedRankingProfileModel rankingProfile,
            int limit)
        {
            var snapshotAt = cursor.SnapshotAt;
            var normalizedProfile = rankingProfile.Copy().Normalize(rankingProfile.ProfileKey);
            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);

            var recentEngagementThreshold = snapshotAt.AddDays(-normalizedProfile.RecentEngagementWindowDays);
            var hotAffinityThreshold = snapshotAt.AddDays(-normalizedProfile.AffinityHotWindowDays);
            var warmAffinityThreshold = snapshotAt.AddDays(-normalizedProfile.AffinityWarmWindowDays);
            var freshnessDay1Threshold = snapshotAt.AddHours(-normalizedProfile.FreshnessDay1Hours);
            var freshnessDay3Threshold = snapshotAt.AddHours(-normalizedProfile.FreshnessDay3Hours);
            var freshnessDay7Threshold = snapshotAt.AddHours(-normalizedProfile.FreshnessDay7Hours);
            var freshnessDay14Threshold = snapshotAt.AddHours(-normalizedProfile.FreshnessDay14Hours);
            var freshnessDay30Threshold = snapshotAt.AddHours(-normalizedProfile.FreshnessDay30Hours);
            var candidateLimit = normalizedProfile.ResolveCandidateLimit(limit);

            var followedIdsQuery = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);
            var visiblePostsQuery = BuildVisibleFeedPostsQuery(currentId, hiddenAccountIds, followedIdsQuery, snapshotAt);
            var rankedPosts = new List<RankedFeedPostRow>();
            var remainingLimit = limit;
            var activeWindowCursorCreatedAt = cursor.WindowCursorCreatedAt;
            var activeWindowCursorPostId = cursor.WindowCursorPostId;
            var activeRankingCursor = cursor.HasPosition ? cursor : null;

            while (remainingLimit > 0)
            {
                var windowCursorCreatedAt = activeWindowCursorCreatedAt;
                var windowCursorPostId = activeWindowCursorPostId;

                var candidatePostsQuery = visiblePostsQuery;
                if (windowCursorCreatedAt.HasValue && windowCursorPostId.HasValue)
                {
                    var boundaryCreatedAt = windowCursorCreatedAt.Value;
                    var boundaryPostId = windowCursorPostId.Value;
                    candidatePostsQuery = candidatePostsQuery.Where(p =>
                        p.CreatedAt < boundaryCreatedAt ||
                        (p.CreatedAt == boundaryCreatedAt &&
                         p.PostId.CompareTo(boundaryPostId) < 0));
                }

                var candidateQuery = candidatePostsQuery
                    .OrderByDescending(p => p.CreatedAt)
                    .ThenByDescending(p => p.PostId)
                    .Take(candidateLimit)
                    .Select(p => new FeedCandidatePostRow
                    {
                        PostId = p.PostId,
                        PostCode = p.PostCode,
                        Privacy = p.Privacy,
                        CreatedAt = p.CreatedAt,
                        AuthorAccountId = p.Account.AccountId,
                        RawJitterHash = normalizedProfile.JitterBucketCount == 1
                            ? 0
                            : AppDbContext.HashTextExtended(p.PostCode, cursor.SessionSeed)
                    });

                var candidatePosts = await candidateQuery.ToListAsync();
                if (candidatePosts.Count == 0)
                {
                    break;
                }

                var windowTail = candidatePosts[^1];
                var candidatePostIds = candidatePosts
                    .Select(x => x.PostId)
                    .ToList();
                var candidateAuthorIds = candidatePosts
                    .Select(x => x.AuthorAccountId)
                    .Distinct()
                    .ToList();

                var followedAuthorIdSet = candidateAuthorIds.Count == 0
                    ? new HashSet<Guid>()
                    : (await _context.Follows
                        .AsNoTracking()
                        .Where(f =>
                            f.FollowerId == currentId &&
                            candidateAuthorIds.Contains(f.FollowedId))
                        .Select(f => f.FollowedId)
                        .ToListAsync())
                    .ToHashSet();

                var reactAggregateLookup = candidatePostIds.Count == 0
                    ? new Dictionary<Guid, FeedPostReactAggregateRow>()
                    : (await _context.PostReacts
                        .AsNoTracking()
                        .Where(r =>
                            candidatePostIds.Contains(r.PostId) &&
                            r.Account.Status == AccountStatusEnum.Active &&
                            SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId))
                        .GroupBy(r => r.PostId)
                        .Select(g => new FeedPostReactAggregateRow
                        {
                            PostId = g.Key,
                            TotalCount = g.Count(),
                            RecentCount = g.Count(x => x.CreatedAt >= recentEngagementThreshold),
                            IsCurrentUserReacted = g.Any(x => x.AccountId == currentId)
                        })
                        .ToListAsync())
                    .ToDictionary(x => x.PostId);

                var commentAggregateLookup = candidatePostIds.Count == 0
                    ? new Dictionary<Guid, FeedPostCommentAggregateRow>()
                    : (await _context.Comments
                        .AsNoTracking()
                        .Where(c =>
                            candidatePostIds.Contains(c.PostId) &&
                            c.Account.Status == AccountStatusEnum.Active &&
                            !hiddenAccountIds.Contains(c.AccountId) &&
                            SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId))
                        .GroupBy(c => c.PostId)
                        .Select(g => new FeedPostCommentAggregateRow
                        {
                            PostId = g.Key,
                            RootCount = g.Count(x => x.ParentCommentId == null),
                            ReplyCount = g.Count(x => x.ParentCommentId != null),
                            RecentRootCount = g.Count(x =>
                                x.ParentCommentId == null &&
                                x.CreatedAt >= recentEngagementThreshold),
                            RecentReplyCount = g.Count(x =>
                                x.ParentCommentId != null &&
                                x.CreatedAt >= recentEngagementThreshold)
                        })
                        .ToListAsync())
                    .ToDictionary(x => x.PostId);

                var authorInteractionLookup = candidateAuthorIds.Count == 0
                    ? new Dictionary<Guid, DateTime>()
                    : (await _context.PostReacts
                        .AsNoTracking()
                        .Where(r =>
                            r.AccountId == currentId &&
                            r.CreatedAt >= warmAffinityThreshold &&
                            candidateAuthorIds.Contains(r.Post.AccountId))
                        .Select(r => new FeedAuthorInteractionSnapshotRow
                        {
                            AuthorAccountId = r.Post.AccountId,
                            LastInteractionAt = r.CreatedAt
                        })
                        .Concat(
                            _context.Comments
                                .AsNoTracking()
                                .Where(c =>
                                    c.AccountId == currentId &&
                                    c.CreatedAt >= warmAffinityThreshold &&
                                    candidateAuthorIds.Contains(c.Post.AccountId))
                                .Select(c => new FeedAuthorInteractionSnapshotRow
                                {
                                    AuthorAccountId = c.Post.AccountId,
                                    LastInteractionAt = c.CreatedAt
                                }))
                        .GroupBy(x => x.AuthorAccountId)
                        .Select(g => new FeedAuthorInteractionSnapshotRow
                        {
                            AuthorAccountId = g.Key,
                            LastInteractionAt = g.Max(x => x.LastInteractionAt)
                        })
                        .ToListAsync())
                    .ToDictionary(x => x.AuthorAccountId, x => x.LastInteractionAt);

                IEnumerable<RankedFeedPostRow> scoredPosts = candidatePosts
                    .Select(x =>
                    {
                        var isOwner = x.AuthorAccountId == currentId;
                        var isFollowedAuthor = followedAuthorIdSet.Contains(x.AuthorAccountId);
                        reactAggregateLookup.TryGetValue(x.PostId, out var reactStats);
                        commentAggregateLookup.TryGetValue(x.PostId, out var commentStats);
                        authorInteractionLookup.TryGetValue(x.AuthorAccountId, out var lastInteractionAt);
                        var hasHotAffinity =
                            !isOwner &&
                            lastInteractionAt >= hotAffinityThreshold;
                        var hasWarmAffinity =
                            !isOwner &&
                            lastInteractionAt >= warmAffinityThreshold;

                        return new RankedFeedPostRow
                        {
                        PostId = x.PostId,
                        PostCode = x.PostCode,
                        Content = null,
                        Privacy = x.Privacy,
                        FeedAspectRatio = AspectRatioEnum.Original,
                        CreatedAt = x.CreatedAt,
                        AuthorAccountId = x.AuthorAccountId,
                        AuthorUsername = string.Empty,
                        AuthorFullName = string.Empty,
                        AuthorAvatarUrl = null,
                        AuthorStatus = AccountStatusEnum.Active,
                        MediaCount = 0,
                        ReactCount = reactStats?.TotalCount ?? 0,
                        CommentCount = commentStats?.RootCount ?? 0,
                        ReplyCount = commentStats?.ReplyCount ?? 0,
                        IsReactedByCurrentUser = reactStats?.IsCurrentUserReacted ?? false,
                        IsSavedByCurrentUser = false,
                        IsOwner = isOwner,
                        IsFollowedAuthor = isFollowedAuthor,
                        IsFollowRequestPendingAuthor = false,
                        RankingWindowCursorCreatedAt = windowCursorCreatedAt,
                        RankingWindowCursorPostId = windowCursorPostId,
                        RankingJitterRank = normalizedProfile.JitterBucketCount == 1
                            ? 0
                            : Math.Abs(x.RawJitterHash % normalizedProfile.JitterBucketCount),
                        RankingScore = ComputeFeedRankingScore(
                            x.CreatedAt,
                            x.Privacy,
                            isOwner,
                            isFollowedAuthor,
                            hasHotAffinity,
                            hasWarmAffinity,
                            reactStats?.RecentCount ?? 0,
                            commentStats?.RecentRootCount ?? 0,
                            commentStats?.RecentReplyCount ?? 0,
                            normalizedProfile,
                            freshnessDay1Threshold,
                            freshnessDay3Threshold,
                            freshnessDay7Threshold,
                            freshnessDay14Threshold,
                            freshnessDay30Threshold),
                        LegacyScore = 0
                        };
                    });

                if (activeRankingCursor?.HasPosition == true)
                {
                    scoredPosts = scoredPosts.Where(x => IsBehindRankingCursor(x, activeRankingCursor));
                }

                var windowItems = scoredPosts
                    .OrderByDescending(x => x.RankingScore)
                    .ThenByDescending(x => x.RankingJitterRank)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.PostId)
                    .Take(remainingLimit)
                    .ToList();

                if (windowItems.Count > 0)
                {
                    rankedPosts.AddRange(windowItems);
                    remainingLimit -= windowItems.Count;
                }

                activeRankingCursor = null;
                activeWindowCursorCreatedAt = windowTail.CreatedAt;
                activeWindowCursorPostId = windowTail.PostId;
            }

            return await HydrateFeedPostsAsync(rankedPosts, currentId, snapshotAt);
        }

        private async Task<List<PostFeedModel>> HydrateFeedPostsAsync(
            List<RankedFeedPostRow> rankedPosts,
            Guid currentId,
            DateTime? snapshotAt)
        {
            if (rankedPosts.Count == 0)
            {
                return new List<PostFeedModel>();
            }

            var rankedPostIds = rankedPosts
                .Select(x => x.PostId)
                .ToList();

            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            var followedIdsQuery = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            var postBaseLookup = await BuildVisibleFeedPostsQuery(currentId, hiddenAccountIds, followedIdsQuery, snapshotAt)
                .Where(p => rankedPostIds.Contains(p.PostId))
                .Select(p => new FeedHydratedPostRow
                {
                    PostId = p.PostId,
                    PostCode = p.PostCode,
                    Content = p.Content,
                    Privacy = p.Privacy,
                    FeedAspectRatio = p.FeedAspectRatio,
                    CreatedAt = p.CreatedAt,
                    AuthorAccountId = p.Account.AccountId,
                    AuthorUsername = p.Account.Username,
                    AuthorFullName = p.Account.FullName,
                    AuthorAvatarUrl = p.Account.AvatarUrl,
                    AuthorStatus = p.Account.Status
                })
                .ToDictionaryAsync(x => x.PostId);

            if (postBaseLookup.Count == 0)
            {
                return new List<PostFeedModel>();
            }

            var visibleRankedPosts = rankedPosts
                .Where(x => postBaseLookup.ContainsKey(x.PostId))
                .ToList();
            if (visibleRankedPosts.Count == 0)
            {
                return new List<PostFeedModel>();
            }

            var topPostIds = visibleRankedPosts
                .Select(x => x.PostId)
                .ToList();
            var topAuthorIds = postBaseLookup.Values
                .Select(x => x.AuthorAccountId)
                .Distinct()
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

            var savedPostIdSet = topPostIds.Count == 0
                ? new HashSet<Guid>()
                : (await _context.PostSaves
                    .AsNoTracking()
                    .Where(s =>
                        s.AccountId == currentId &&
                        topPostIds.Contains(s.PostId))
                    .Select(s => s.PostId)
                    .ToListAsync())
                .ToHashSet();

            var taggedAccounts = await _context.PostTags
                .AsNoTracking()
                .Where(t =>
                    topPostIds.Contains(t.PostId) &&
                    t.TaggedAccount.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(t.TaggedAccount.RoleId))
                .Select(t => new
                {
                    t.PostId,
                    t.TaggedAccountId,
                    t.TaggedAccount.Username,
                    t.TaggedAccount.FullName,
                    t.TaggedAccount.AvatarUrl,
                    t.CreatedAt
                })
                .ToListAsync();

            var taggedAccountIds = taggedAccounts
                .Select(x => x.TaggedAccountId)
                .Distinct()
                .ToList();
            var accountIdsForFollowRequest = taggedAccountIds
                .Concat(topAuthorIds)
                .Distinct()
                .ToList();

            var followingIdSet = taggedAccountIds.Count == 0
                ? new HashSet<Guid>()
                : (await _context.Follows
                    .AsNoTracking()
                    .Where(f =>
                        f.FollowerId == currentId &&
                        taggedAccountIds.Contains(f.FollowedId))
                    .Select(f => f.FollowedId)
                    .ToListAsync())
                .ToHashSet();

            var authorAndTaggedFollowRequestIdSet = accountIdsForFollowRequest.Count == 0
                ? new HashSet<Guid>()
                : (await _context.FollowRequests
                    .AsNoTracking()
                    .Where(fr =>
                        fr.RequesterId == currentId &&
                        accountIdsForFollowRequest.Contains(fr.TargetId))
                    .Select(fr => fr.TargetId)
                    .ToListAsync())
                .ToHashSet();

            var followerIdSet = taggedAccountIds.Count == 0
                ? new HashSet<Guid>()
                : (await _context.Follows
                    .AsNoTracking()
                    .Where(f =>
                        f.FollowedId == currentId &&
                        taggedAccountIds.Contains(f.FollowerId))
                    .Select(f => f.FollowerId)
                    .ToListAsync())
                .ToHashSet();

            var taggedAccountLookup = taggedAccounts
                .GroupBy(x => x.PostId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Total = g.Count(),
                        IsCurrentUserTagged = g.Any(x => x.TaggedAccountId == currentId),
                        Preview = g
                            .OrderByDescending(x => x.TaggedAccountId == currentId)
                            .ThenByDescending(x => followingIdSet.Contains(x.TaggedAccountId))
                            .ThenByDescending(x => authorAndTaggedFollowRequestIdSet.Contains(x.TaggedAccountId))
                            .ThenByDescending(x => followerIdSet.Contains(x.TaggedAccountId))
                            .ThenBy(x => x.CreatedAt)
                            .Select(x => new PostTaggedAccountModel
                            {
                                AccountId = x.TaggedAccountId,
                                Username = x.Username,
                                FullName = x.FullName,
                                AvatarUrl = x.AvatarUrl,
                                IsFollowing = followingIdSet.Contains(x.TaggedAccountId),
                                IsFollowRequested = authorAndTaggedFollowRequestIdSet.Contains(x.TaggedAccountId),
                                IsFollower = followerIdSet.Contains(x.TaggedAccountId)
                            })
                            .Take(2)
                            .ToList()
                    });

            return visibleRankedPosts
                .Select(x =>
                {
                    var hydratedPost = postBaseLookup.TryGetValue(x.PostId, out var postBase)
                        ? postBase
                        : null;
                    var postMedias = mediaLookup.TryGetValue(x.PostId, out var mediaItems)
                        ? mediaItems
                        : new List<MediaPostPersonalListModel>();

                    var postTagInfo = taggedAccountLookup.TryGetValue(x.PostId, out var tagItems)
                        ? tagItems
                        : null;

                    return new PostFeedModel
                    {
                        PostId = x.PostId,
                        PostCode = hydratedPost?.PostCode ?? x.PostCode,
                        Content = hydratedPost?.Content,
                        Privacy = hydratedPost?.Privacy ?? x.Privacy,
                        FeedAspectRatio = hydratedPost?.FeedAspectRatio ?? x.FeedAspectRatio,
                        CreatedAt = hydratedPost?.CreatedAt ?? x.CreatedAt,
                        Author = new AccountOnFeedModel
                        {
                            AccountId = hydratedPost?.AuthorAccountId ?? x.AuthorAccountId,
                            Username = hydratedPost?.AuthorUsername ?? x.AuthorUsername,
                            FullName = hydratedPost?.AuthorFullName ?? x.AuthorFullName,
                            AvatarUrl = hydratedPost?.AuthorAvatarUrl ?? x.AuthorAvatarUrl,
                            Status = hydratedPost?.AuthorStatus ?? x.AuthorStatus,
                            IsFollowedByCurrentUser = x.IsOwner || x.IsFollowedAuthor,
                            IsFollowRequestPendingByCurrentUser = !x.IsOwner && !x.IsFollowedAuthor && authorAndTaggedFollowRequestIdSet.Contains(hydratedPost?.AuthorAccountId ?? x.AuthorAccountId)
                        },
                        Medias = postMedias,
                        TaggedAccountsPreview = postTagInfo?.Preview
                            ?? new List<PostTaggedAccountModel>(),
                        TotalTaggedAccounts = postTagInfo?.Total ?? 0,
                        IsCurrentUserTagged = postTagInfo?.IsCurrentUserTagged ?? false,
                        MediaCount = x.MediaCount > 0 ? x.MediaCount : postMedias.Count,
                        ReactCount = x.ReactCount,
                        CommentCount = x.CommentCount,
                        ReplyCount = x.ReplyCount,
                        IsFollowedAuthor = x.IsFollowedAuthor,
                        IsReactedByCurrentUser = x.IsReactedByCurrentUser,
                        IsSavedByCurrentUser = savedPostIdSet.Contains(x.PostId),
                        IsOwner = x.IsOwner,
                        RankingScore = x.RankingScore,
                        RankingJitterRank = x.RankingJitterRank,
                        RankingWindowCursorCreatedAt = x.RankingWindowCursorCreatedAt,
                        RankingWindowCursorPostId = x.RankingWindowCursorPostId
                    };
                })
                .ToList();
        }

        private IQueryable<Post> BuildVisibleFeedPostsQuery(
            Guid currentId,
            IQueryable<Guid> hiddenAccountIds,
            IQueryable<Guid> followedIdsQuery,
            DateTime? snapshotAt = null)
        {
            var query = _context.Posts
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    p.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(p.AccountId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId) &&
                    p.Medias.Any() &&
                    (
                        p.Privacy == PostPrivacyEnum.Public ||
                        (p.Privacy == PostPrivacyEnum.FollowOnly && followedIdsQuery.Contains(p.AccountId)) ||
                        p.AccountId == currentId
                    ));

            if (snapshotAt.HasValue)
            {
                var boundarySnapshotAt = snapshotAt.Value;
                query = query.Where(p => p.CreatedAt <= boundarySnapshotAt);
            }

            return query;
        }

        private sealed class RankedFeedPostRow
        {
            public Guid PostId { get; set; }
            public string PostCode { get; set; } = string.Empty;
            public string? Content { get; set; }
            public PostPrivacyEnum Privacy { get; set; }
            public AspectRatioEnum FeedAspectRatio { get; set; }
            public DateTime CreatedAt { get; set; }
            public Guid AuthorAccountId { get; set; }
            public string AuthorUsername { get; set; } = string.Empty;
            public string AuthorFullName { get; set; } = string.Empty;
            public string? AuthorAvatarUrl { get; set; }
            public AccountStatusEnum AuthorStatus { get; set; }
            public int MediaCount { get; set; }
            public int ReactCount { get; set; }
            public int CommentCount { get; set; }
            public int ReplyCount { get; set; }
            public bool IsReactedByCurrentUser { get; set; }
            public bool IsSavedByCurrentUser { get; set; }
            public bool IsOwner { get; set; }
            public bool IsFollowedAuthor { get; set; }
            public bool IsFollowRequestPendingAuthor { get; set; }
            public decimal RankingScore { get; set; }
            public long RankingJitterRank { get; set; }
            public DateTime? RankingWindowCursorCreatedAt { get; set; }
            public Guid? RankingWindowCursorPostId { get; set; }
            public double LegacyScore { get; set; }
        }

        private sealed class FeedCandidatePostRow
        {
            public Guid PostId { get; set; }
            public string PostCode { get; set; } = string.Empty;
            public PostPrivacyEnum Privacy { get; set; }
            public DateTime CreatedAt { get; set; }
            public Guid AuthorAccountId { get; set; }
            public long RawJitterHash { get; set; }
        }

        private sealed class FeedHydratedPostRow
        {
            public Guid PostId { get; set; }
            public string PostCode { get; set; } = string.Empty;
            public string? Content { get; set; }
            public PostPrivacyEnum Privacy { get; set; }
            public AspectRatioEnum FeedAspectRatio { get; set; }
            public DateTime CreatedAt { get; set; }
            public Guid AuthorAccountId { get; set; }
            public string AuthorUsername { get; set; } = string.Empty;
            public string AuthorFullName { get; set; } = string.Empty;
            public string? AuthorAvatarUrl { get; set; }
            public AccountStatusEnum AuthorStatus { get; set; }
        }

        private sealed class FeedPostReactAggregateRow
        {
            public Guid PostId { get; set; }
            public int TotalCount { get; set; }
            public int RecentCount { get; set; }
            public bool IsCurrentUserReacted { get; set; }
        }

        private sealed class FeedPostCommentAggregateRow
        {
            public Guid PostId { get; set; }
            public int RootCount { get; set; }
            public int ReplyCount { get; set; }
            public int RecentRootCount { get; set; }
            public int RecentReplyCount { get; set; }
        }

        private sealed class FeedAuthorInteractionSnapshotRow
        {
            public Guid AuthorAccountId { get; set; }
            public DateTime LastInteractionAt { get; set; }
        }

        private static decimal ComputeFeedRankingScore(
            DateTime createdAt,
            PostPrivacyEnum privacy,
            bool isOwner,
            bool isFollowedAuthor,
            bool hasHotAffinity,
            bool hasWarmAffinity,
            int recentReactCount,
            int recentRootCommentCount,
            int recentReplyCount,
            FeedRankingProfileModel normalizedProfile,
            DateTime freshnessDay1Threshold,
            DateTime freshnessDay3Threshold,
            DateTime freshnessDay7Threshold,
            DateTime freshnessDay14Threshold,
            DateTime freshnessDay30Threshold)
        {
            var freshnessScore = createdAt >= freshnessDay1Threshold
                ? normalizedProfile.FreshnessDay1Score
                : createdAt >= freshnessDay3Threshold
                    ? normalizedProfile.FreshnessDay3Score
                    : createdAt >= freshnessDay7Threshold
                        ? normalizedProfile.FreshnessDay7Score
                        : createdAt >= freshnessDay14Threshold
                            ? normalizedProfile.FreshnessDay14Score
                            : createdAt >= freshnessDay30Threshold
                                ? normalizedProfile.FreshnessDay30Score
                                : normalizedProfile.FreshnessOlderScore;

            var affinityScore = hasHotAffinity
                ? normalizedProfile.AffinityHotBonus
                : hasWarmAffinity
                    ? normalizedProfile.AffinityWarmBonus
                    : 0m;

            return freshnessScore
                + (isFollowedAuthor ? normalizedProfile.FollowBonus : 0m)
                + (isOwner ? normalizedProfile.SelfFallbackBonus : 0m)
                + (!isOwner && !isFollowedAuthor && privacy == PostPrivacyEnum.Public
                    ? normalizedProfile.DiscoverBonus
                    : 0m)
                + affinityScore
                + Math.Min(recentReactCount, normalizedProfile.ReactCountCap) * normalizedProfile.ReactWeight
                + Math.Min(recentRootCommentCount, normalizedProfile.RootCommentCountCap) * normalizedProfile.RootCommentWeight
                + Math.Min(recentReplyCount, normalizedProfile.ReplyCountCap) * normalizedProfile.ReplyWeight;
        }

        private static bool IsBehindRankingCursor(RankedFeedPostRow rankedPost, PostFeedCursorModel cursor)
        {
            var cursorScore = cursor.Score!.Value;
            var cursorJitterRank = cursor.JitterRank!.Value;
            var cursorCreatedAt = cursor.CreatedAt!.Value;
            var cursorPostId = cursor.PostId!.Value;

            return rankedPost.RankingScore < cursorScore ||
                   (rankedPost.RankingScore == cursorScore &&
                    (rankedPost.RankingJitterRank < cursorJitterRank ||
                     (rankedPost.RankingJitterRank == cursorJitterRank &&
                      (rankedPost.CreatedAt < cursorCreatedAt ||
                       (rankedPost.CreatedAt == cursorCreatedAt &&
                        rankedPost.PostId.CompareTo(cursorPostId) < 0)))));
        }

        private sealed class PostTaggedAccountRawModel
        {
            public Guid AccountId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string? AvatarUrl { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
