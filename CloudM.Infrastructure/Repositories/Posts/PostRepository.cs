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

            var baseQuery = _context.Posts.AsNoTracking()
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
                    )
                );

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

            return await HydrateFeedPostsAsync(topScoredPosts, currentId);
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

            var followedIdsQuery = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            var recentEngagementThreshold = snapshotAt.AddDays(-normalizedProfile.RecentEngagementWindowDays);
            var hotAffinityThreshold = snapshotAt.AddDays(-normalizedProfile.AffinityHotWindowDays);
            var warmAffinityThreshold = snapshotAt.AddDays(-normalizedProfile.AffinityWarmWindowDays);
            var freshnessDay1Threshold = snapshotAt.AddHours(-normalizedProfile.FreshnessDay1Hours);
            var freshnessDay3Threshold = snapshotAt.AddHours(-normalizedProfile.FreshnessDay3Hours);
            var freshnessDay7Threshold = snapshotAt.AddHours(-normalizedProfile.FreshnessDay7Hours);
            var freshnessDay14Threshold = snapshotAt.AddHours(-normalizedProfile.FreshnessDay14Hours);
            var freshnessDay30Threshold = snapshotAt.AddHours(-normalizedProfile.FreshnessDay30Hours);
            var candidateLimit = normalizedProfile.ResolveCandidateLimit(limit);

            var visiblePostsQuery = _context.Posts
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    p.CreatedAt <= snapshotAt &&
                    p.Account.Status == AccountStatusEnum.Active &&
                    !hiddenAccountIds.Contains(p.AccountId) &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId) &&
                    p.Medias.Any() &&
                    (
                        p.Privacy == PostPrivacyEnum.Public ||
                        (p.Privacy == PostPrivacyEnum.FollowOnly && followedIdsQuery.Contains(p.AccountId)) ||
                        p.AccountId == currentId
                    ));
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
                        RecentReactCount = p.Reacts.Count(r =>
                            r.CreatedAt >= recentEngagementThreshold &&
                            r.Account.Status == AccountStatusEnum.Active &&
                            SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                        RecentRootCommentCount = p.Comments.Count(c =>
                            c.ParentCommentId == null &&
                            c.CreatedAt >= recentEngagementThreshold &&
                            c.Account.Status == AccountStatusEnum.Active &&
                            !hiddenAccountIds.Contains(c.AccountId) &&
                            SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId)),
                        RecentReplyCount = p.Comments.Count(c =>
                            c.ParentCommentId != null &&
                            c.CreatedAt >= recentEngagementThreshold &&
                            c.Account.Status == AccountStatusEnum.Active &&
                            !hiddenAccountIds.Contains(c.AccountId) &&
                            SocialRoleRules.SocialEligibleRoleIds.Contains(c.Account.RoleId)),
                        IsReactedByCurrentUser = p.Reacts.Any(r => r.AccountId == currentId && r.Account.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(r.Account.RoleId)),
                        IsSavedByCurrentUser = _context.PostSaves.Any(s => s.PostId == p.PostId && s.AccountId == currentId),
                        IsOwner = p.AccountId == currentId,
                        IsFollowedAuthor = followedIdsQuery.Contains(p.AccountId),
                        IsFollowRequestPendingAuthor = _context.FollowRequests.Any(fr => fr.RequesterId == currentId && fr.TargetId == p.AccountId),
                        HasHotAffinity =
                            p.AccountId != currentId &&
                            (
                                _context.PostReacts.Any(r =>
                                    r.AccountId == currentId &&
                                    r.CreatedAt >= hotAffinityThreshold &&
                                    r.Post.AccountId == p.AccountId) ||
                                _context.Comments.Any(c =>
                                    c.AccountId == currentId &&
                                    c.CreatedAt >= hotAffinityThreshold &&
                                    c.Post.AccountId == p.AccountId)
                            ),
                        HasWarmAffinity =
                            p.AccountId != currentId &&
                            (
                                _context.PostReacts.Any(r =>
                                    r.AccountId == currentId &&
                                    r.CreatedAt >= warmAffinityThreshold &&
                                    r.Post.AccountId == p.AccountId) ||
                                _context.Comments.Any(c =>
                                    c.AccountId == currentId &&
                                    c.CreatedAt >= warmAffinityThreshold &&
                                    c.Post.AccountId == p.AccountId)
                            ),
                        RawJitterHash = normalizedProfile.JitterBucketCount == 1
                            ? 0
                            : AppDbContext.HashTextExtended(p.PostCode, cursor.SessionSeed)
                    });

                var windowTail = await candidateQuery
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.PostId)
                    .Select(x => new
                    {
                        x.CreatedAt,
                        x.PostId
                    })
                    .FirstOrDefaultAsync();

                if (windowTail == null)
                {
                    break;
                }

                var scoredQuery = candidateQuery
                    .Select(x => new RankedFeedPostRow
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
                        RankingWindowCursorCreatedAt = windowCursorCreatedAt,
                        RankingWindowCursorPostId = windowCursorPostId,
                        RankingJitterRank = normalizedProfile.JitterBucketCount == 1
                            ? 0
                            : Math.Abs(x.RawJitterHash % normalizedProfile.JitterBucketCount),
                        RankingScore =
                            (x.CreatedAt >= freshnessDay1Threshold
                                ? normalizedProfile.FreshnessDay1Score
                                : x.CreatedAt >= freshnessDay3Threshold
                                    ? normalizedProfile.FreshnessDay3Score
                                    : x.CreatedAt >= freshnessDay7Threshold
                                        ? normalizedProfile.FreshnessDay7Score
                                        : x.CreatedAt >= freshnessDay14Threshold
                                            ? normalizedProfile.FreshnessDay14Score
                                            : x.CreatedAt >= freshnessDay30Threshold
                                                ? normalizedProfile.FreshnessDay30Score
                                                : normalizedProfile.FreshnessOlderScore)
                            + (x.IsFollowedAuthor ? normalizedProfile.FollowBonus : 0m)
                            + (x.IsOwner ? normalizedProfile.SelfFallbackBonus : 0m)
                            + (!x.IsOwner && !x.IsFollowedAuthor && x.Privacy == PostPrivacyEnum.Public
                                ? normalizedProfile.DiscoverBonus
                                : 0m)
                            + (x.HasHotAffinity
                                ? normalizedProfile.AffinityHotBonus
                                : x.HasWarmAffinity
                                    ? normalizedProfile.AffinityWarmBonus
                                    : 0m)
                            + (x.RecentReactCount > normalizedProfile.ReactCountCap
                                ? normalizedProfile.ReactCountCap
                                : x.RecentReactCount) * normalizedProfile.ReactWeight
                            + (x.RecentRootCommentCount > normalizedProfile.RootCommentCountCap
                                ? normalizedProfile.RootCommentCountCap
                                : x.RecentRootCommentCount) * normalizedProfile.RootCommentWeight
                            + (x.RecentReplyCount > normalizedProfile.ReplyCountCap
                                ? normalizedProfile.ReplyCountCap
                                : x.RecentReplyCount) * normalizedProfile.ReplyWeight,
                        LegacyScore = 0
                    });

                if (activeRankingCursor?.HasPosition == true)
                {
                    var cursorScore = activeRankingCursor.Score!.Value;
                    var cursorJitterRank = activeRankingCursor.JitterRank!.Value;
                    var cursorCreatedAtValue = activeRankingCursor.CreatedAt!.Value;
                    var cursorPostIdValue = activeRankingCursor.PostId!.Value;

                    scoredQuery = scoredQuery.Where(x =>
                        x.RankingScore < cursorScore ||
                        (x.RankingScore == cursorScore &&
                         (x.RankingJitterRank < cursorJitterRank ||
                          (x.RankingJitterRank == cursorJitterRank &&
                           (x.CreatedAt < cursorCreatedAtValue ||
                            (x.CreatedAt == cursorCreatedAtValue &&
                             x.PostId.CompareTo(cursorPostIdValue) < 0))))));
                }

                var windowItems = await scoredQuery
                    .OrderByDescending(x => x.RankingScore)
                    .ThenByDescending(x => x.RankingJitterRank)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.PostId)
                    .Take(remainingLimit)
                    .ToListAsync();

                if (windowItems.Count > 0)
                {
                    rankedPosts.AddRange(windowItems);
                    remainingLimit -= windowItems.Count;
                }

                activeRankingCursor = null;
                activeWindowCursorCreatedAt = windowTail.CreatedAt;
                activeWindowCursorPostId = windowTail.PostId;
            }

            return await HydrateFeedPostsAsync(rankedPosts, currentId);
        }

        private async Task<List<PostFeedModel>> HydrateFeedPostsAsync(List<RankedFeedPostRow> rankedPosts, Guid currentId)
        {
            if (rankedPosts.Count == 0)
            {
                return new List<PostFeedModel>();
            }

            var topPostIds = rankedPosts
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

            var followRequestIdSet = taggedAccountIds.Count == 0
                ? new HashSet<Guid>()
                : (await _context.FollowRequests
                    .AsNoTracking()
                    .Where(fr =>
                        fr.RequesterId == currentId &&
                        taggedAccountIds.Contains(fr.TargetId))
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
                            .ThenByDescending(x => followRequestIdSet.Contains(x.TaggedAccountId))
                            .ThenByDescending(x => followerIdSet.Contains(x.TaggedAccountId))
                            .ThenBy(x => x.CreatedAt)
                            .Select(x => new PostTaggedAccountModel
                            {
                                AccountId = x.TaggedAccountId,
                                Username = x.Username,
                                FullName = x.FullName,
                                AvatarUrl = x.AvatarUrl,
                                IsFollowing = followingIdSet.Contains(x.TaggedAccountId),
                                IsFollowRequested = followRequestIdSet.Contains(x.TaggedAccountId),
                                IsFollower = followerIdSet.Contains(x.TaggedAccountId)
                            })
                            .Take(2)
                            .ToList()
                    });

            return rankedPosts
                .Select(x =>
                {
                    var postMedias = mediaLookup.TryGetValue(x.PostId, out var mediaItems)
                        ? mediaItems
                        : new List<MediaPostPersonalListModel>();

                    var postTagInfo = taggedAccountLookup.TryGetValue(x.PostId, out var tagItems)
                        ? tagItems
                        : null;

                    return new PostFeedModel
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
                            IsFollowedByCurrentUser = x.IsOwner || x.IsFollowedAuthor,
                            IsFollowRequestPendingByCurrentUser = !x.IsOwner && !x.IsFollowedAuthor && x.IsFollowRequestPendingAuthor
                        },
                        Medias = postMedias,
                        TaggedAccountsPreview = postTagInfo?.Preview
                            ?? new List<PostTaggedAccountModel>(),
                        TotalTaggedAccounts = postTagInfo?.Total ?? 0,
                        IsCurrentUserTagged = postTagInfo?.IsCurrentUserTagged ?? false,
                        MediaCount = x.MediaCount,
                        ReactCount = x.ReactCount,
                        CommentCount = x.CommentCount,
                        ReplyCount = x.ReplyCount,
                        IsFollowedAuthor = x.IsFollowedAuthor,
                        IsReactedByCurrentUser = x.IsReactedByCurrentUser,
                        IsSavedByCurrentUser = x.IsSavedByCurrentUser,
                        IsOwner = x.IsOwner,
                        RankingScore = x.RankingScore,
                        RankingJitterRank = x.RankingJitterRank,
                        RankingWindowCursorCreatedAt = x.RankingWindowCursorCreatedAt,
                        RankingWindowCursorPostId = x.RankingWindowCursorPostId
                    };
                })
                .ToList();
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
