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
using System.Xml.Linq;

namespace CloudM.Infrastructure.Repositories.Accounts
{
    public class AccountRepository : IAccountRepository
    {
        private const int DefaultInviteSearchLimit = 10;
        private const int MaxInviteSearchLimit = 50;
        private const int MinInviteSearchKeywordLength = 2;
        private const int EmptyKeywordRecentDirectChatCount = 5;
        private const int RecentDirectChatPrefetch = 80;
        private const int InviteSearchPrefetchMultiplier = 6;
        private const int MinInviteSearchPrefetch = 60;
        private const int MaxInviteSearchPrefetch = 240;
        private const double FuzzySimilarityThreshold = 0.24d;
        private const int DefaultPostShareSearchLimit = 20;
        private const int MaxPostShareSearchLimit = 50;
        private const int MaxPostShareSearchPrefetch = 300;
        private const int MinSidebarSearchKeywordLength = 1;
        private const int DefaultSidebarSearchLimit = 20;
        private const int MaxSidebarSearchLimit = 30;
        private const int SidebarSearchPrefetchMultiplier = 6;
        private const int MinSidebarSearchPrefetch = 60;
        private const int MaxSidebarSearchPrefetch = 240;
        private const int ShortSidebarSearchPrefetchMultiplier = 2;
        private const int MinShortSidebarSearchPrefetch = 20;
        private const int MaxShortSidebarSearchPrefetch = 60;
        private const int DefaultPostTagSearchLimit = 10;
        private const int MaxPostTagSearchLimit = 30;
        private const int PostTagSearchPrefetchMultiplier = 6;
        private const int MinPostTagSearchPrefetch = 60;
        private const int MaxPostTagSearchPrefetch = 240;
        private const int EmptyKeywordPostTagPrefetch = 120;
        private const int HomeFollowSuggestionJitterBucketCount = 7;
        private const int PageFollowSuggestionJitterBucketCount = 3;

        private readonly AppDbContext _context;
        public AccountRepository(AppDbContext context)
        {
            _context = context;
        }

        private IQueryable<Account> GetSocialAccountsQuery()
        {
            return _context.Accounts
                .Where(a =>
                    a.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(a.RoleId));
        }

        private IQueryable<Account> GetSocialAccountsNoTrackingQuery()
        {
            return GetSocialAccountsQuery().AsNoTracking();
        }
        public async Task<bool> IsUsernameExist (string username)
        {
            var normalizedUsername = (username ?? string.Empty).Trim().ToLower();
            return await _context.Accounts.AnyAsync(a => a.Username == normalizedUsername);
        }
        public async Task<bool> IsEmailExist(string email)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLower();
            return await _context.Accounts.AnyAsync(a => a.Email == normalizedEmail);
        }
        public async Task<bool> IsAccountIdExist(Guid accountId)
        {
            return await _context.Accounts.AnyAsync(a =>
                a.AccountId == accountId &&
                a.Status == AccountStatusEnum.Active &&
                SocialRoleRules.SocialEligibleRoleIds.Contains(a.RoleId));
        }
        public async Task AddAccount(Account account)
        {
            await _context.Accounts.AddAsync(account);
        }
        public async Task<Account?> GetAccountById(Guid accountId)
        {
            return await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Settings)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);
        }
        public async Task<Account?> GetAccountByEmail(string email)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLower();
            return await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Settings)
                .FirstOrDefaultAsync(a => a.Email == normalizedEmail);
        }
        public Task UpdateAccount(Account account)
        {
            _context.Accounts.Update(account);
            return Task.CompletedTask;
        }
        public async Task<Account?> GetAccountByUsername(string username)
        {
            var normalizedUsername = (username ?? string.Empty).Trim().ToLower();
            return await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Settings)
                .FirstOrDefaultAsync(a => a.Username == normalizedUsername);
        }
        public async Task<Account?> GetByRefreshToken(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            return await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Settings)
                .FirstOrDefaultAsync(a => a.RefreshToken == refreshToken);
        }
        // search and filter accounts (admin)
        public async Task<(List<Account> Items, int TotalItems)> GetAccountsAsync(Guid? id, string? username, string? email,
            string? fullname, string? phone, int? roleId, bool? gender, AccountStatusEnum? status, int page, int pageSize)
        {
            var query = _context.Accounts.Include(a => a.Role).OrderBy(a => a.CreatedAt).AsQueryable();
            if (id.HasValue && id.Value != Guid.Empty)
            {
                query = query.Where(a => a.AccountId == id);
            }
            if (!string.IsNullOrWhiteSpace(username))
            {
                query = query.Where(a => a.Username.ToLower().Contains(username.ToLower()));
            }
            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(a => a.Email.ToLower().Contains(email.ToLower()));
            }
            if (!string.IsNullOrWhiteSpace(fullname))
            {
                var words = fullname.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    var searchPattern = $"%{word}%";
                    query = query.Where(a => EF.Functions.ILike(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(searchPattern)));
                }
            }
            if (!string.IsNullOrWhiteSpace(phone))
            {
                query = query.Where(a => a.Phone != null && a.Phone.Contains(phone));
            }
            if (roleId.HasValue)
            {
                query = query.Where(a => a.RoleId == roleId.Value);
            }
            if (gender.HasValue)
            {
                query = query.Where(a => a.Gender == gender.Value);
            } 
            if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }
            int totalItems = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalItems);
        }

        public async Task<AccountProfilePreviewModel?> GetProfilePreviewAsync(Guid targetId, Guid? currentId)
        {
            var query = GetSocialAccountsNoTrackingQuery()
                .Where(a => a.AccountId == targetId && a.Status == AccountStatusEnum.Active);

            if (currentId.HasValue && currentId.Value != targetId)
            {
                var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId.Value);
                query = query.Where(a => !hiddenAccountIds.Contains(a.AccountId));
            }

            var data = await query
                .Select(a => new
                {
                    Account = new AccountBasicInfoModel
                    {
                        AccountId = a.AccountId,
                        Username = a.Username,
                        FullName = a.FullName,
                        AvatarUrl = a.AvatarUrl,
                        Bio = a.Bio,
                        CoverUrl = a.CoverUrl,
                        Status = a.Status
                    },
                    PostCount = a.Posts.Count(p => !p.IsDeleted && p.Medias.Any() && SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId)),
                    FollowerCount = a.Followers.Count(f => f.Follower.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId)),
                    FollowingCount = a.Followings.Count(f => f.Followed.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(f.Followed.RoleId)),

                    IsCurrentUser = currentId.HasValue && a.AccountId == currentId.Value,
                    IsFollowedByCurrentUser =
                        currentId.HasValue && a.Followers.Any(f => f.FollowerId == currentId.Value),
                    IsFollowRequestPendingByCurrentUser =
                        currentId.HasValue && _context.FollowRequests.Any(fr =>
                            fr.RequesterId == currentId.Value && fr.TargetId == a.AccountId),

                    RecentMedias = a.Posts
                        .Where(p =>
                            !p.IsDeleted &&
                            p.Privacy == PostPrivacyEnum.Public &&
                            p.Medias.Any() 
                        )
                        .OrderByDescending(p => p.CreatedAt)
                        .Take(3)
                        .Select(p => p.Medias
                            .OrderBy(m => m.CreatedAt)
                            .Select(m => new PostMediaProfilePreviewModel
                            {
                                MediaId = m.MediaId,
                                PostId = p.PostId,
                                PostCode = p.PostCode,
                                MediaUrl = m.MediaUrl,
                                MediaType = m.Type
                            })
                            .First()
                        )
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (data == null) return null;

            return new AccountProfilePreviewModel
            {
                Account = data.Account,
                PostCount = data.PostCount,
                FollowerCount = data.FollowerCount,
                FollowingCount = data.FollowingCount,
                IsCurrentUser = data.IsCurrentUser,
                IsFollowedByCurrentUser = data.IsFollowedByCurrentUser || data.IsCurrentUser,
                IsFollowRequestPendingByCurrentUser = !data.IsCurrentUser && !data.IsFollowedByCurrentUser && data.IsFollowRequestPendingByCurrentUser,
                RecentPosts = data.RecentMedias
            };
        }


        public async Task<ProfileInfoModel?> GetProfileInfoAsync(Guid targetId, Guid? currentId)
        {
            var query = GetSocialAccountsNoTrackingQuery()
                .Where(a => a.AccountId == targetId && a.Status == AccountStatusEnum.Active);

            if (currentId.HasValue && currentId.Value != targetId)
            {
                var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId.Value);
                query = query.Where(a => !hiddenAccountIds.Contains(a.AccountId));
            }

            var data = await query
                .Select(a => new
                {
                    a.AccountId,
                    a.Username,
                    a.Email,
                    a.FullName,
                    a.AvatarUrl,
                    a.Phone,
                    a.Bio,
                    a.CoverUrl,
                    a.Gender,
                    a.Address,
                    a.CreatedAt,
                    PostCount = a.Posts.Count(p => !p.IsDeleted && p.Medias.Any() && SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId)),
                    FollowerCount = a.Followers.Count(f => f.Follower.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId)),
                    FollowingCount = a.Followings.Count(f => f.Followed.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(f.Followed.RoleId)),
                    IsFollowedByCurrentUser = currentId.HasValue && a.Followers.Any(f => f.FollowerId == currentId.Value),
                    IsFollowRequestPendingByCurrentUser =
                        currentId.HasValue && _context.FollowRequests.Any(fr =>
                            fr.RequesterId == currentId.Value && fr.TargetId == a.AccountId),
                    Settings = a.Settings
                })
                .FirstOrDefaultAsync();

            if (data == null) return null;

            var s = data.Settings;

            return new ProfileInfoModel
            {
                AccountId = data.AccountId,
                Username = data.Username,
                Email = data.Email,
                FullName = data.FullName,
                AvatarUrl = data.AvatarUrl,
                Phone = data.Phone,
                Bio = data.Bio,
                CoverUrl = data.CoverUrl,
                Gender = data.Gender,
                Address = data.Address,
                CreatedAt = data.CreatedAt,
                PostCount = data.PostCount,
                FollowerCount = data.FollowerCount,
                FollowingCount = data.FollowingCount,
                IsCurrentUser = currentId.HasValue && data.AccountId == currentId.Value,
                IsFollowedByCurrentUser = data.IsFollowedByCurrentUser,
                IsFollowRequestPendingByCurrentUser = data.IsFollowRequestPendingByCurrentUser,

                // virtual defaults
                PhonePrivacy = s?.PhonePrivacy ?? AccountPrivacyEnum.Private,
                AddressPrivacy = s?.AddressPrivacy ?? AccountPrivacyEnum.Private,
                DefaultPostPrivacy = s?.DefaultPostPrivacy ?? PostPrivacyEnum.Public,
                FollowerPrivacy = s?.FollowerPrivacy ?? AccountPrivacyEnum.Public,
                FollowingPrivacy = s?.FollowingPrivacy ?? AccountPrivacyEnum.Public,
                FollowPrivacy = s?.FollowPrivacy ?? FollowPrivacyEnum.Anyone,
                StoryHighlightPrivacy = s?.StoryHighlightPrivacy ?? AccountPrivacyEnum.Public,
                GroupChatInvitePermission = s?.GroupChatInvitePermission ?? GroupChatInvitePermissionEnum.Anyone,
                OnlineStatusVisibility = s?.OnlineStatusVisibility ?? OnlineStatusVisibilityEnum.ContactsOnly,
                TagPermission = s?.TagPermission == TagPermissionEnum.NoOne
                    ? TagPermissionEnum.NoOne
                    : TagPermissionEnum.Anyone,
            };
        }

        public async Task<ProfileInfoModel?> GetProfileInfoByUsernameAsync(string username, Guid? currentId)
        {
            var normalizedUsername = (username ?? string.Empty).Trim().ToLower();
            var query = GetSocialAccountsNoTrackingQuery()
                .Where(a => a.Username == normalizedUsername && a.Status == AccountStatusEnum.Active);

            if (currentId.HasValue)
            {
                var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId.Value);
                query = query.Where(a => !hiddenAccountIds.Contains(a.AccountId));
            }

            var data = await query
                .Select(a => new
                {
                    a.AccountId,
                    a.Username,
                    a.Email,
                    a.FullName,
                    a.AvatarUrl,
                    a.Phone,
                    a.Bio,
                    a.CoverUrl,
                    a.Gender,
                    a.Address,
                    a.CreatedAt,
                    PostCount = a.Posts.Count(p => !p.IsDeleted && p.Medias.Any() && SocialRoleRules.SocialEligibleRoleIds.Contains(p.Account.RoleId)),
                    FollowerCount = a.Followers.Count(f => f.Follower.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId)),
                    FollowingCount = a.Followings.Count(f => f.Followed.Status == AccountStatusEnum.Active && SocialRoleRules.SocialEligibleRoleIds.Contains(f.Followed.RoleId)),
                    IsFollowedByCurrentUser = currentId.HasValue && a.Followers.Any(f => f.FollowerId == currentId.Value),
                    IsFollowRequestPendingByCurrentUser =
                        currentId.HasValue && _context.FollowRequests.Any(fr =>
                            fr.RequesterId == currentId.Value && fr.TargetId == a.AccountId),
                    Settings = a.Settings
                })
                .FirstOrDefaultAsync();

            if (data == null) return null;

            var s = data.Settings;

            return new ProfileInfoModel
            {
                AccountId = data.AccountId,
                Username = data.Username,
                Email = data.Email,
                FullName = data.FullName,
                AvatarUrl = data.AvatarUrl,
                Phone = data.Phone,
                Bio = data.Bio,
                CoverUrl = data.CoverUrl,
                Gender = data.Gender,
                Address = data.Address,
                CreatedAt = data.CreatedAt,
                PostCount = data.PostCount,
                FollowerCount = data.FollowerCount,
                FollowingCount = data.FollowingCount,
                IsCurrentUser = currentId.HasValue && data.AccountId == currentId.Value,
                IsFollowedByCurrentUser = data.IsFollowedByCurrentUser,
                IsFollowRequestPendingByCurrentUser = data.IsFollowRequestPendingByCurrentUser,

                // virtual defaults
                PhonePrivacy = s?.PhonePrivacy ?? AccountPrivacyEnum.Private,
                AddressPrivacy = s?.AddressPrivacy ?? AccountPrivacyEnum.Private,
                DefaultPostPrivacy = s?.DefaultPostPrivacy ?? PostPrivacyEnum.Public,
                FollowerPrivacy = s?.FollowerPrivacy ?? AccountPrivacyEnum.Public,
                FollowingPrivacy = s?.FollowingPrivacy ?? AccountPrivacyEnum.Public,
                FollowPrivacy = s?.FollowPrivacy ?? FollowPrivacyEnum.Anyone,
                StoryHighlightPrivacy = s?.StoryHighlightPrivacy ?? AccountPrivacyEnum.Public,
                GroupChatInvitePermission = s?.GroupChatInvitePermission ?? GroupChatInvitePermissionEnum.Anyone,
                OnlineStatusVisibility = s?.OnlineStatusVisibility ?? OnlineStatusVisibilityEnum.ContactsOnly,
                TagPermission = s?.TagPermission == TagPermissionEnum.NoOne
                    ? TagPermissionEnum.NoOne
                    : TagPermissionEnum.Anyone
            };
        }

        public async Task<List<GroupInviteAccountSearchModel>> SearchAccountsForGroupInviteAsync(
            Guid currentId,
            string keyword,
            IEnumerable<Guid>? excludeAccountIds,
            int limit = 10)
        {
            var excludeIds = (excludeAccountIds ?? Enumerable.Empty<Guid>())
                .Where(id => id != Guid.Empty && id != currentId)
                .Distinct()
                .ToList();

            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            if (normalizedKeyword.Length == 0)
            {
                return await GetRecentDirectChatInviteCandidatesAsync(currentId, excludeIds, EmptyKeywordRecentDirectChatCount);
            }

            if (normalizedKeyword.Length < MinInviteSearchKeywordLength)
            {
                return new List<GroupInviteAccountSearchModel>();
            }

            var safeLimit = NormalizeInviteSearchLimit(limit);
            var prefetchLimit = Math.Min(
                Math.Max(safeLimit * InviteSearchPrefetchMultiplier, MinInviteSearchPrefetch),
                MaxInviteSearchPrefetch);

            var containsPattern = $"%{normalizedKeyword}%";
            var startsWithPattern = $"{normalizedKeyword}%";

            var query = GetSocialAccountsNoTrackingQuery()
                .Where(a => a.AccountId != currentId);

            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            query = query.Where(a => !hiddenAccountIds.Contains(a.AccountId));

            if (excludeIds.Count > 0)
            {
                query = query.Where(a => !excludeIds.Contains(a.AccountId));
            }

            var preliminaryCandidates = await query
                .Select(a => new PreliminaryInviteSearchCandidate
                {
                    AccountId = a.AccountId,
                    Username = a.Username,
                    FullName = a.FullName,
                    AvatarUrl = a.AvatarUrl,
                    InvitePermission = a.Settings != null
                        ? a.Settings.GroupChatInvitePermission
                        : GroupChatInvitePermissionEnum.Anyone,
                    IsFollowing = _context.Follows.Any(f => f.FollowerId == currentId && f.FollowedId == a.AccountId),
                    IsFollower = _context.Follows.Any(f => f.FollowerId == a.AccountId && f.FollowedId == currentId),
                    UsernameStartsWith = EF.Functions.ILike(a.Username, startsWithPattern),
                    FullNameStartsWith = EF.Functions.ILike(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(startsWithPattern)),
                    UsernameContains = EF.Functions.ILike(a.Username, containsPattern),
                    FullNameContains = EF.Functions.ILike(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(containsPattern)),
                    UsernameSimilarity = AppDbContext.Similarity(a.Username, normalizedKeyword),
                    FullNameSimilarity = AppDbContext.Similarity(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(normalizedKeyword))
                })
                .Where(x =>
                    x.InvitePermission == GroupChatInvitePermissionEnum.Anyone ||
                    (x.InvitePermission == GroupChatInvitePermissionEnum.FollowersOrFollowing && (x.IsFollowing || x.IsFollower)))
                .Where(x =>
                    x.UsernameContains ||
                    x.FullNameContains ||
                    x.UsernameSimilarity >= FuzzySimilarityThreshold ||
                    x.FullNameSimilarity >= FuzzySimilarityThreshold)
                .OrderByDescending(x => x.UsernameStartsWith)
                .ThenByDescending(x => x.FullNameStartsWith)
                .ThenByDescending(x => x.UsernameContains)
                .ThenByDescending(x => x.FullNameContains)
                .ThenByDescending(x => x.UsernameSimilarity)
                .ThenByDescending(x => x.FullNameSimilarity)
                .Take(prefetchLimit)
                .ToListAsync();

            if (preliminaryCandidates.Count == 0)
            {
                return new List<GroupInviteAccountSearchModel>();
            }

            var candidateIds = preliminaryCandidates
                .Select(x => x.AccountId)
                .Distinct()
                .ToList();

            var directChatRows = await _context.Conversations
                .AsNoTracking()
                .Where(c => !c.IsDeleted && !c.IsGroup)
                .Where(c => c.Members.Any(m => m.AccountId == currentId && !m.HasLeft))
                .Where(c => c.Members.Any(m => candidateIds.Contains(m.AccountId) && !m.HasLeft))
                .Select(c => new
                {
                    OtherAccountId = c.Members
                        .Where(m => m.AccountId != currentId && candidateIds.Contains(m.AccountId) && !m.HasLeft)
                        .OrderBy(m => m.JoinedAt)
                        .ThenBy(m => m.AccountId)
                        .Select(m => m.AccountId)
                        .FirstOrDefault(),
                    LastMessageAt = c.Messages.Select(m => (DateTime?)m.SentAt).Max()
                })
                .Where(x => x.OtherAccountId != Guid.Empty && x.LastMessageAt.HasValue)
                .ToListAsync();

            var lastDirectMessageMap = directChatRows
                .GroupBy(x => x.OtherAccountId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.LastMessageAt)!.Value);

            var myGroupConversationIdsQuery = _context.ConversationMembers
                .AsNoTracking()
                .Where(cm => cm.AccountId == currentId && !cm.HasLeft && cm.Conversation.IsGroup && !cm.Conversation.IsDeleted)
                .Select(cm => cm.ConversationId);

            var mutualGroupRows = await _context.ConversationMembers
                .AsNoTracking()
                .Where(cm => candidateIds.Contains(cm.AccountId) && !cm.HasLeft && cm.Conversation.IsGroup && !cm.Conversation.IsDeleted)
                .Where(cm => myGroupConversationIdsQuery.Contains(cm.ConversationId))
                .GroupBy(cm => cm.AccountId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var mutualGroupCountMap = mutualGroupRows.ToDictionary(x => x.AccountId, x => x.Count);

            var nowUtc = DateTime.UtcNow;

            var rankedResults = preliminaryCandidates
                .Select(candidate =>
                {
                    var matchScore = ComputeMatchScore(candidate);
                    var followingScore = candidate.IsFollowing ? 500d : 0d;
                    var followerScore = candidate.IsFollower ? 420d : 0d;

                    lastDirectMessageMap.TryGetValue(candidate.AccountId, out var lastDirectMessageAt);
                    var recentChatScore = ComputeRecentChatScore(lastDirectMessageAt, nowUtc);

                    mutualGroupCountMap.TryGetValue(candidate.AccountId, out var mutualGroupCount);
                    var mutualGroupScore = ComputeMutualGroupScore(mutualGroupCount);

                    var totalScore = matchScore + followingScore + followerScore + recentChatScore + mutualGroupScore;

                    return new GroupInviteAccountSearchModel
                    {
                        AccountId = candidate.AccountId,
                        Username = candidate.Username,
                        FullName = candidate.FullName,
                        AvatarUrl = candidate.AvatarUrl,
                        IsFollowing = candidate.IsFollowing,
                        IsFollower = candidate.IsFollower,
                        MutualGroupCount = mutualGroupCount,
                        LastDirectMessageAt = lastDirectMessageAt,
                        MatchScore = matchScore,
                        FollowingScore = followingScore,
                        FollowerScore = followerScore,
                        RecentChatScore = recentChatScore,
                        MutualGroupScore = mutualGroupScore,
                        TotalScore = totalScore
                    };
                })
                .OrderByDescending(x => x.TotalScore)
                .ThenByDescending(x => x.MatchScore)
                .ThenByDescending(x => x.RecentChatScore)
                .ThenByDescending(x => x.IsFollowing)
                .ThenByDescending(x => x.IsFollower)
                .ThenByDescending(x => x.MutualGroupCount)
                .ThenByDescending(x => x.LastDirectMessageAt)
                .ThenBy(x => x.Username)
                .Take(safeLimit)
                .ToList();

            return rankedResults;
        }

        public async Task<List<PostShareAccountSearchModel>> SearchAccountsForPostShareAsync(
            Guid currentId,
            string keyword,
            int limit = 20)
        {
            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            if (normalizedKeyword.Length == 0)
            {
                return new List<PostShareAccountSearchModel>();
            }

            var safeLimit = NormalizePostShareSearchLimit(limit);
            var prefetchLimit = Math.Min(Math.Max(safeLimit * 6, safeLimit), MaxPostShareSearchPrefetch);
            var containsPattern = $"%{normalizedKeyword}%";
            var startsWithPattern = $"{normalizedKeyword}%";

            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);

            var preliminaryCandidates = await GetSocialAccountsNoTrackingQuery()
                .Where(a => a.AccountId != currentId)
                .Where(a => !hiddenAccountIds.Contains(a.AccountId))
                .Select(a => new PreliminaryPostShareAccountCandidate
                {
                    AccountId = a.AccountId,
                    Username = a.Username,
                    FullName = a.FullName,
                    AvatarUrl = a.AvatarUrl,
                    UsernameStartsWith = EF.Functions.ILike(a.Username, startsWithPattern),
                    FullNameStartsWith = EF.Functions.ILike(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(startsWithPattern)),
                    UsernameContains = EF.Functions.ILike(a.Username, containsPattern),
                    FullNameContains = EF.Functions.ILike(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(containsPattern)),
                    UsernameSimilarity = AppDbContext.Similarity(a.Username, normalizedKeyword),
                    FullNameSimilarity = AppDbContext.Similarity(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(normalizedKeyword))
                })
                .Where(x =>
                    x.UsernameContains ||
                    x.FullNameContains ||
                    x.UsernameSimilarity >= FuzzySimilarityThreshold ||
                    x.FullNameSimilarity >= FuzzySimilarityThreshold)
                .OrderByDescending(x => x.UsernameStartsWith)
                .ThenByDescending(x => x.FullNameStartsWith)
                .ThenByDescending(x => x.UsernameContains)
                .ThenByDescending(x => x.FullNameContains)
                .ThenByDescending(x => x.UsernameSimilarity)
                .ThenByDescending(x => x.FullNameSimilarity)
                .ThenBy(x => x.Username)
                .Take(prefetchLimit)
                .ToListAsync();

            if (preliminaryCandidates.Count == 0)
            {
                return new List<PostShareAccountSearchModel>();
            }

            var candidateIds = preliminaryCandidates
                .Select(x => x.AccountId)
                .Distinct()
                .ToList();

            var directChatRows = await _context.Conversations
                .AsNoTracking()
                .Where(c => !c.IsDeleted && !c.IsGroup)
                .Where(c => c.Members.Any(m => m.AccountId == currentId && !m.HasLeft))
                .Where(c => c.Members.Any(m => candidateIds.Contains(m.AccountId) && !m.HasLeft))
                .Select(c => new
                {
                    OtherAccountId = c.Members
                        .Where(m => m.AccountId != currentId && candidateIds.Contains(m.AccountId) && !m.HasLeft)
                        .OrderBy(m => m.JoinedAt)
                        .ThenBy(m => m.AccountId)
                        .Select(m => m.AccountId)
                        .FirstOrDefault(),
                    LastMessageAt = c.Messages.Select(m => (DateTime?)m.SentAt).Max()
                })
                .Where(x => x.OtherAccountId != Guid.Empty)
                .ToListAsync();

            var directChatMap = directChatRows
                .GroupBy(x => x.OtherAccountId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Max(x => x.LastMessageAt)
                );

            var results = preliminaryCandidates
                .Select(candidate =>
                {
                    directChatMap.TryGetValue(candidate.AccountId, out var lastContactedAt);
                    var isContacted = directChatMap.ContainsKey(candidate.AccountId);

                    return new PostShareAccountSearchModel
                    {
                        AccountId = candidate.AccountId,
                        Username = candidate.Username,
                        FullName = candidate.FullName,
                        AvatarUrl = candidate.AvatarUrl,
                        IsContacted = isContacted,
                        LastContactedAt = lastContactedAt,
                        MatchScore = ComputePostShareAccountMatchScore(candidate)
                    };
                })
                .OrderByDescending(x => x.MatchScore)
                .ThenByDescending(x => x.IsContacted)
                .ThenByDescending(x => x.LastContactedAt)
                .ThenBy(x => x.Username)
                .Take(safeLimit)
                .ToList();

            return results;
        }

        public async Task<List<PostTagAccountSearchModel>> SearchAccountsForPostTagAsync(
            Guid currentId,
            Guid? visibilityOwnerId,
            string keyword,
            PostPrivacyEnum? postPrivacy,
            IEnumerable<Guid>? excludeAccountIds,
            int limit = 10)
        {
            if (postPrivacy == PostPrivacyEnum.Private)
            {
                return new List<PostTagAccountSearchModel>();
            }

            var requireFollowerVisibility = postPrivacy == PostPrivacyEnum.FollowOnly;
            var effectiveVisibilityOwnerId = visibilityOwnerId.HasValue && visibilityOwnerId.Value != Guid.Empty
                ? visibilityOwnerId.Value
                : currentId;
            var excludeIds = (excludeAccountIds ?? Enumerable.Empty<Guid>())
                .Where(id => id != Guid.Empty && id != currentId)
                .Distinct()
                .ToList();

            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            var safeLimit = NormalizePostTagSearchLimit(limit);

            if (normalizedKeyword.Length == 0)
            {
                return new List<PostTagAccountSearchModel>();
            }

            var prefetchLimit = Math.Min(
                Math.Max(safeLimit * PostTagSearchPrefetchMultiplier, MinPostTagSearchPrefetch),
                MaxPostTagSearchPrefetch);

            var containsPattern = $"%{normalizedKeyword}%";
            var startsWithPattern = $"{normalizedKeyword}%";

            var query = GetSocialAccountsNoTrackingQuery()
                .Where(a => a.AccountId != currentId);

            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            query = query.Where(a => !hiddenAccountIds.Contains(a.AccountId));

            if (excludeIds.Count > 0)
            {
                query = query.Where(a => !excludeIds.Contains(a.AccountId));
            }

            var preliminaryCandidates = await query
                .Select(a => new PreliminaryPostTagSearchCandidate
                {
                    AccountId = a.AccountId,
                    Username = a.Username,
                    FullName = a.FullName,
                    AvatarUrl = a.AvatarUrl,
                    TagPermission = a.Settings != null
                        ? a.Settings.TagPermission
                        : TagPermissionEnum.Anyone,
                    IsFollowing = _context.Follows.Any(f => f.FollowerId == currentId && f.FollowedId == a.AccountId),
                    IsFollower = _context.Follows.Any(f => f.FollowerId == a.AccountId && f.FollowedId == currentId),
                    IsVisibilityFollower = _context.Follows.Any(f => f.FollowerId == a.AccountId && f.FollowedId == effectiveVisibilityOwnerId),
                    UsernameStartsWith = EF.Functions.ILike(a.Username, startsWithPattern),
                    FullNameStartsWith = EF.Functions.ILike(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(startsWithPattern)),
                    UsernameContains = EF.Functions.ILike(a.Username, containsPattern),
                    FullNameContains = EF.Functions.ILike(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(containsPattern)),
                    UsernameSimilarity = AppDbContext.Similarity(a.Username, normalizedKeyword),
                    FullNameSimilarity = AppDbContext.Similarity(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(normalizedKeyword))
                })
                .Where(x => x.TagPermission != TagPermissionEnum.NoOne)
                .Where(x => !requireFollowerVisibility || x.IsVisibilityFollower || x.AccountId == effectiveVisibilityOwnerId)
                .Where(x =>
                    x.UsernameContains ||
                    x.FullNameContains ||
                    x.UsernameSimilarity >= FuzzySimilarityThreshold ||
                    x.FullNameSimilarity >= FuzzySimilarityThreshold)
                .OrderByDescending(x => x.UsernameStartsWith)
                .ThenByDescending(x => x.FullNameStartsWith)
                .ThenByDescending(x => x.UsernameContains)
                .ThenByDescending(x => x.FullNameContains)
                .ThenByDescending(x => x.UsernameSimilarity)
                .ThenByDescending(x => x.FullNameSimilarity)
                .ThenBy(x => x.Username)
                .Take(prefetchLimit)
                .ToListAsync();

            if (preliminaryCandidates.Count == 0)
            {
                return new List<PostTagAccountSearchModel>();
            }

            var candidateIds = preliminaryCandidates
                .Select(x => x.AccountId)
                .Distinct()
                .ToList();

            var directChatRows = await _context.Conversations
                .AsNoTracking()
                .Where(c => !c.IsDeleted && !c.IsGroup)
                .Where(c => c.Members.Any(m => m.AccountId == currentId && !m.HasLeft))
                .Where(c => c.Members.Any(m => candidateIds.Contains(m.AccountId) && !m.HasLeft))
                .Select(c => new
                {
                    OtherAccountId = c.Members
                        .Where(m => m.AccountId != currentId && candidateIds.Contains(m.AccountId) && !m.HasLeft)
                        .OrderBy(m => m.JoinedAt)
                        .ThenBy(m => m.AccountId)
                        .Select(m => m.AccountId)
                        .FirstOrDefault(),
                    LastMessageAt = c.Messages.Select(m => (DateTime?)m.SentAt).Max()
                })
                .Where(x => x.OtherAccountId != Guid.Empty && x.LastMessageAt.HasValue)
                .ToListAsync();

            var lastDirectMessageMap = directChatRows
                .GroupBy(x => x.OtherAccountId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.LastMessageAt)!.Value);

            var nowUtc = DateTime.UtcNow;

            var rankedResults = preliminaryCandidates
                .Select(candidate =>
                {
                    var matchScore = ComputePostTagMatchScore(candidate);
                    var followingScore = candidate.IsFollowing ? 520d : 0d;
                    var followerScore = candidate.IsFollower ? 200d : 0d;

                    lastDirectMessageMap.TryGetValue(candidate.AccountId, out var lastContactedAt);
                    var recentChatScore = ComputeRecentChatScore(lastContactedAt, nowUtc);

                    var totalScore = matchScore + followingScore + followerScore + recentChatScore;

                    return new PostTagAccountSearchModel
                    {
                        AccountId = candidate.AccountId,
                        Username = candidate.Username,
                        FullName = candidate.FullName,
                        AvatarUrl = candidate.AvatarUrl,
                        IsFollowing = candidate.IsFollowing,
                        IsFollower = candidate.IsFollower,
                        LastContactedAt = lastContactedAt,
                        MatchScore = matchScore,
                        FollowingScore = followingScore,
                        FollowerScore = followerScore,
                        RecentChatScore = recentChatScore,
                        TotalScore = totalScore
                    };
                })
                .OrderByDescending(x => x.TotalScore)
                .ThenByDescending(x => x.MatchScore)
                .ThenByDescending(x => x.RecentChatScore)
                .ThenByDescending(x => x.IsFollowing)
                .ThenByDescending(x => x.IsFollower)
                .ThenByDescending(x => x.LastContactedAt)
                .ThenBy(x => x.Username)
                .Take(safeLimit)
                .ToList();

            return rankedResults;
        }

        public async Task<List<Account>> GetAccountsByIds(IEnumerable<Guid> accountIds)
        {
            return await GetSocialAccountsQuery()
                .Include(a => a.Settings)
                .Where(a => accountIds.Contains(a.AccountId))
                .ToListAsync();
        }

        public async Task<List<Account>> GetAccountsByUsernames(IEnumerable<string> usernames)
        {
            var normalizedUsernames = (usernames ?? Enumerable.Empty<string>())
                .Select(x => (x ?? string.Empty).Trim().ToLower())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (normalizedUsernames.Count == 0)
            {
                return new List<Account>();
            }

            return await GetSocialAccountsQuery()
                .Include(a => a.Settings)
                .Where(a => normalizedUsernames.Contains(a.Username))
                .ToListAsync();
        }

        public async Task<List<SidebarAccountSearchModel>> SearchSidebarAccountsAsync(
            Guid currentId,
            string keyword,
            int limit = 20)
        {
            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            if (normalizedKeyword.Length < MinSidebarSearchKeywordLength)
            {
                return new List<SidebarAccountSearchModel>();
            }

            var safeLimit = NormalizeSidebarSearchLimit(limit);
            var prefetchLimit = NormalizeSidebarSearchPrefetchLimit(safeLimit, normalizedKeyword.Length);

            var normalizedUsernameKeyword = normalizedKeyword.ToLowerInvariant();
            var containsPattern = $"%{normalizedKeyword}%";
            var startsWithPattern = $"{normalizedKeyword}%";
            var fullNameWordStartsWithPattern = $"% {normalizedKeyword}%";
            var useBroadContains = normalizedKeyword.Length >= 2;
            var useFuzzySimilarity = SidebarSearchRankingHelper.ShouldUseFuzzySimilarity(normalizedKeyword);
            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);

            var preliminaryCandidates = await GetSocialAccountsNoTrackingQuery()
                .Where(a => a.AccountId != currentId)
                .Where(a => !hiddenAccountIds.Contains(a.AccountId))
                .Select(a => new PreliminarySidebarSearchCandidate
                {
                    AccountId = a.AccountId,
                    Username = a.Username,
                    FullName = a.FullName,
                    AvatarUrl = a.AvatarUrl,
                    IsFollowing = _context.Follows.Any(f => f.FollowerId == currentId && f.FollowedId == a.AccountId),
                    IsFollower = _context.Follows.Any(f => f.FollowerId == a.AccountId && f.FollowedId == currentId),
                    HasSearchHistory = _context.AccountSearchHistories.Any(
                        h => h.CurrentId == currentId && h.TargetId == a.AccountId),
                    UsernameExact = a.Username == normalizedUsernameKeyword,
                    UsernameStartsWith = EF.Functions.ILike(a.Username, startsWithPattern),
                    FullNameStartsWith = EF.Functions.ILike(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(startsWithPattern)),
                    FullNameWordStartsWith = EF.Functions.ILike(
                        AppDbContext.Unaccent(a.FullName),
                        AppDbContext.Unaccent(fullNameWordStartsWithPattern)),
                    UsernameContains = useBroadContains && EF.Functions.ILike(a.Username, containsPattern),
                    FullNameContains = useBroadContains && EF.Functions.ILike(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(containsPattern)),
                    UsernameSimilarity = useFuzzySimilarity
                        ? AppDbContext.Similarity(a.Username, normalizedKeyword)
                        : 0d,
                    FullNameSimilarity = useFuzzySimilarity
                        ? AppDbContext.Similarity(AppDbContext.Unaccent(a.FullName), AppDbContext.Unaccent(normalizedKeyword))
                        : 0d
                })
                .Where(x =>
                    x.UsernameStartsWith ||
                    x.FullNameStartsWith ||
                    x.FullNameWordStartsWith ||
                    (useBroadContains && x.UsernameContains) ||
                    (useBroadContains && x.FullNameContains) ||
                    (useFuzzySimilarity && x.UsernameSimilarity >= SidebarSearchRankingHelper.FuzzySimilarityThreshold) ||
                    (useFuzzySimilarity && x.FullNameSimilarity >= SidebarSearchRankingHelper.FuzzySimilarityThreshold))
                .OrderByDescending(x => x.UsernameExact)
                .ThenByDescending(x => x.UsernameStartsWith)
                .ThenByDescending(x => x.FullNameStartsWith)
                .ThenByDescending(x => x.FullNameWordStartsWith)
                .ThenByDescending(x => x.UsernameContains)
                .ThenByDescending(x => x.FullNameContains)
                .ThenByDescending(x => x.IsFollowing)
                .ThenByDescending(x => x.HasSearchHistory)
                .ThenByDescending(x => x.IsFollower)
                .ThenByDescending(x => x.UsernameSimilarity)
                .ThenByDescending(x => x.FullNameSimilarity)
                .ThenBy(x => x.Username)
                .Take(prefetchLimit)
                .ToListAsync();

            var hasStrongMatches = preliminaryCandidates.Any(IsSidebarStrongMatch);
            preliminaryCandidates = preliminaryCandidates
                .Where(x => hasStrongMatches
                    ? IsSidebarStrongMatch(x)
                    : IsSidebarCandidateEligible(x, normalizedKeyword.Length))
                .ToList();

            if (preliminaryCandidates.Count == 0)
            {
                return new List<SidebarAccountSearchModel>();
            }

            var candidateIds = preliminaryCandidates
                .Select(x => x.AccountId)
                .Distinct()
                .ToList();

            var directChatMap = await GetDirectConversationLastContactMapAsync(currentId, candidateIds);

            var historyRows = await _context.AccountSearchHistories
                .AsNoTracking()
                .Where(x => x.CurrentId == currentId && candidateIds.Contains(x.TargetId))
                .Select(x => new
                {
                    x.TargetId,
                    x.LastSearchedAt
                })
                .ToListAsync();

            var historyMap = historyRows.ToDictionary(x => x.TargetId, x => x.LastSearchedAt);
            var nowUtc = DateTime.UtcNow;

            return preliminaryCandidates
                .Select(candidate =>
                {
                    directChatMap.TryGetValue(candidate.AccountId, out var lastContactedAt);
                    historyMap.TryGetValue(candidate.AccountId, out var lastSearchedAt);

                    var matchScore = ComputeSidebarMatchScore(candidate);
                    var followingScore = candidate.IsFollowing ? 420d : 0d;
                    var followerScore = candidate.IsFollower ? 180d : 0d;
                    var recentChatScore = ComputeRecentChatScore(lastContactedAt, nowUtc);
                    var historyScore = ComputeSidebarHistoryScore(lastSearchedAt, nowUtc);
                    var totalScore = matchScore + followingScore + followerScore + recentChatScore + historyScore;

                    return new
                    {
                        Item = new SidebarAccountSearchModel
                        {
                            AccountId = candidate.AccountId,
                            Username = candidate.Username,
                            FullName = candidate.FullName,
                            AvatarUrl = candidate.AvatarUrl,
                            IsFollowing = candidate.IsFollowing,
                            IsFollower = candidate.IsFollower,
                            HasDirectConversation = directChatMap.ContainsKey(candidate.AccountId),
                            LastContactedAt = lastContactedAt,
                            LastSearchedAt = lastSearchedAt
                        },
                        TotalScore = totalScore,
                        MatchScore = matchScore,
                        RecentChatScore = recentChatScore,
                        lastContactedAt
                    };
                })
                .OrderByDescending(x => x.TotalScore)
                .ThenByDescending(x => x.MatchScore)
                .ThenByDescending(x => x.RecentChatScore)
                .ThenByDescending(x => x.lastContactedAt)
                .ThenBy(x => x.Item.Username)
                .Select(x => x.Item)
                .Take(safeLimit)
                .ToList();
        }

        public async Task<(List<FollowSuggestionCandidateModel> Items, int TotalItems)> GetFollowSuggestionsAsync(
            Guid currentId,
            int page,
            int pageSize,
            bool prioritizeDiscovery)
        {
            var normalizedPage = page <= 0 ? 1 : page;
            var normalizedPageSize = pageSize <= 0 ? 10 : pageSize;

            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            var contactTargetIdsQuery = _context.ConversationMembers
                .AsNoTracking()
                .Where(cm =>
                    cm.AccountId == currentId &&
                    !cm.HasLeft &&
                    !cm.IsDeleted &&
                    !cm.Conversation.IsDeleted)
                .SelectMany(cm => cm.Conversation.Members
                    .Where(m =>
                        !m.HasLeft &&
                        !m.IsDeleted &&
                        m.AccountId != currentId)
                    .Select(m => m.AccountId))
                .Distinct();

            var myFollowingIdsQuery = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            var myPendingRequestTargetIdsQuery = _context.FollowRequests
                .AsNoTracking()
                .Where(fr => fr.RequesterId == currentId)
                .Select(fr => fr.TargetId);

            var followerIdsQuery = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowedId == currentId)
                .Select(f => f.FollowerId);

            var mutualConnectionCountsQuery = _context.Follows
                .AsNoTracking()
                .Where(f =>
                    myFollowingIdsQuery.Contains(f.FollowerId) &&
                    f.Follower.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId))
                .GroupBy(f => f.FollowedId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    Count = g.Count()
                });

            var followerCountsQuery = _context.Follows
                .AsNoTracking()
                .Where(f =>
                    f.Follower.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId))
                .GroupBy(f => f.FollowedId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    Count = g.Count()
                });

            var eligibleAccountsBaseQuery = GetSocialAccountsNoTrackingQuery()
                .Where(a => a.AccountId != currentId)
                .Where(a => !hiddenAccountIds.Contains(a.AccountId))
                .Where(a => !myFollowingIdsQuery.Contains(a.AccountId))
                .Where(a => !myPendingRequestTargetIdsQuery.Contains(a.AccountId));

            var eligibleAccountsQuery = eligibleAccountsBaseQuery;
            List<Guid>? prefetchedHomeCandidateIds = null;
            if (prioritizeDiscovery)
            {
                var homeSignalTake = Math.Max(normalizedPageSize * 6, 24);
                var homeFallbackTake = Math.Max(normalizedPageSize * 10, 60);

                var homeContactCandidateIdsQuery = eligibleAccountsBaseQuery
                    .Where(a => contactTargetIdsQuery.Contains(a.AccountId))
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => a.AccountId)
                    .Take(homeSignalTake);

                var homeFollowerCandidateIdsQuery = eligibleAccountsBaseQuery
                    .Where(a => followerIdsQuery.Contains(a.AccountId))
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => a.AccountId)
                    .Take(homeSignalTake);

                var homeMutualCandidateIdsQuery = (
                    from a in eligibleAccountsBaseQuery
                    join mutual in mutualConnectionCountsQuery on a.AccountId equals mutual.AccountId
                    orderby mutual.Count descending, a.CreatedAt descending
                    select a.AccountId)
                    .Take(homeSignalTake);

                var homePopularCandidateIdsQuery = (
                    from a in eligibleAccountsBaseQuery
                    join followers in followerCountsQuery on a.AccountId equals followers.AccountId
                    orderby followers.Count descending, a.CreatedAt descending
                    select a.AccountId)
                    .Take(homeSignalTake);

                var homeFreshCandidateIdsQuery = eligibleAccountsBaseQuery
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => a.AccountId)
                    .Take(homeFallbackTake);

                var homeCandidateIdsQuery = homeContactCandidateIdsQuery
                    .Union(homeFollowerCandidateIdsQuery)
                    .Union(homeMutualCandidateIdsQuery)
                    .Union(homePopularCandidateIdsQuery)
                    .Union(homeFreshCandidateIdsQuery);

                prefetchedHomeCandidateIds = await homeCandidateIdsQuery
                    .Distinct()
                    .ToListAsync();

                if (prefetchedHomeCandidateIds.Count == 0)
                {
                    return (new List<FollowSuggestionCandidateModel>(), 0);
                }

                eligibleAccountsQuery = eligibleAccountsBaseQuery
                    .Where(a => prefetchedHomeCandidateIds.Contains(a.AccountId));
            }

            var totalItems = prefetchedHomeCandidateIds?.Count ?? await eligibleAccountsQuery.CountAsync();
            if (totalItems == 0)
            {
                return (new List<FollowSuggestionCandidateModel>(), 0);
            }

            var jitterSeed = CreateFollowSuggestionJitterSeed(currentId, prioritizeDiscovery, DateTime.UtcNow);
            var jitterBucketCount = prioritizeDiscovery
                ? HomeFollowSuggestionJitterBucketCount
                : PageFollowSuggestionJitterBucketCount;

            if (prioritizeDiscovery || ShouldUseInMemorySuggestionFallback())
            {
                var candidateAccounts = await eligibleAccountsQuery
                    .Select(a => new
                    {
                        a.AccountId,
                        a.Username,
                        a.FullName,
                        a.AvatarUrl,
                        a.CreatedAt
                    })
                    .ToListAsync();
                var candidateIds = candidateAccounts
                    .Select(x => x.AccountId)
                    .ToHashSet();

                var myFollowingIds = await myFollowingIdsQuery.ToListAsync();
                var contactTargetIds = await contactTargetIdsQuery
                    .Where(accountId => candidateIds.Contains(accountId))
                    .ToListAsync();
                var followerIds = await followerIdsQuery
                    .Where(accountId => candidateIds.Contains(accountId))
                    .ToListAsync();
                var candidateFollowRows = await _context.Follows
                    .AsNoTracking()
                    .Where(f => candidateIds.Contains(f.FollowedId))
                    .Select(f => new
                    {
                        f.FollowerId,
                        f.FollowedId,
                        FollowerStatus = f.Follower.Status,
                        FollowerRoleId = f.Follower.RoleId
                    })
                    .ToListAsync();

                var contactSet = contactTargetIds.ToHashSet();
                var followerSet = followerIds.ToHashSet();
                var myFollowingSet = myFollowingIds.ToHashSet();
                var followerCounts = candidateFollowRows
                    .Where(x =>
                        x.FollowerStatus == AccountStatusEnum.Active &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(x.FollowerRoleId))
                    .GroupBy(x => x.FollowedId)
                    .ToDictionary(g => g.Key, g => g.Count());
                var mutualCounts = candidateFollowRows
                    .Where(x =>
                        myFollowingSet.Contains(x.FollowerId) &&
                        x.FollowerStatus == AccountStatusEnum.Active &&
                        SocialRoleRules.SocialEligibleRoleIds.Contains(x.FollowerRoleId))
                    .GroupBy(x => x.FollowedId)
                    .ToDictionary(g => g.Key, g => g.Count());

                var rankedCandidates = candidateAccounts
                    .Select(a => new FollowSuggestionCandidateProjection
                    {
                        AccountId = a.AccountId,
                        Username = a.Username,
                        FullName = a.FullName,
                        AvatarUrl = a.AvatarUrl,
                        CreatedAt = a.CreatedAt,
                        IsContact = contactSet.Contains(a.AccountId),
                        IsFollower = followerSet.Contains(a.AccountId),
                        MutualConnectionCount = mutualCounts.GetValueOrDefault(a.AccountId, 0),
                        FollowersCount = followerCounts.GetValueOrDefault(a.AccountId, 0),
                        RawJitterHash = ComputeInMemoryFollowSuggestionJitterHash(a.Username, jitterSeed)
                    })
                    .ToList();

                var orderedCandidates = ApplyFollowSuggestionOrdering(
                    rankedCandidates,
                    prioritizeDiscovery,
                    jitterBucketCount);

                var pagedCandidates = orderedCandidates
                    .Skip((normalizedPage - 1) * normalizedPageSize)
                    .Take(normalizedPageSize)
                    .ToList();

                var lastContactedMap = await GetDirectConversationLastContactMapAsync(
                    currentId,
                    pagedCandidates.Select(x => x.AccountId).ToList());

                var inMemoryItems = pagedCandidates
                    .Select(x => new FollowSuggestionCandidateModel
                    {
                        AccountId = x.AccountId,
                        Username = x.Username,
                        FullName = x.FullName,
                        AvatarUrl = x.AvatarUrl,
                        IsContact = x.IsContact,
                        IsFollower = x.IsFollower,
                        HasDirectConversation = lastContactedMap.ContainsKey(x.AccountId),
                        LastContactedAt = lastContactedMap.GetValueOrDefault(x.AccountId),
                        MutualFollowCount = x.MutualConnectionCount
                    })
                    .ToList();

                return (inMemoryItems, totalItems);
            }

            var rankedCandidatesQuery = eligibleAccountsQuery
                .Select(a => new FollowSuggestionCandidateProjection
                {
                    AccountId = a.AccountId,
                    Username = a.Username,
                    FullName = a.FullName,
                    AvatarUrl = a.AvatarUrl,
                    CreatedAt = a.CreatedAt,
                    IsContact = contactTargetIdsQuery.Contains(a.AccountId),
                    IsFollower = followerIdsQuery.Contains(a.AccountId),
                    MutualConnectionCount = mutualConnectionCountsQuery
                        .Where(x => x.AccountId == a.AccountId)
                        .Select(x => x.Count)
                        .FirstOrDefault(),
                    FollowersCount = followerCountsQuery
                        .Where(x => x.AccountId == a.AccountId)
                        .Select(x => x.Count)
                        .FirstOrDefault(),
                    RawJitterHash = AppDbContext.HashTextExtended(a.Username, jitterSeed)
                });

            var orderedQuery = ApplyFollowSuggestionOrdering(
                rankedCandidatesQuery,
                prioritizeDiscovery,
                jitterBucketCount);

            var items = await orderedQuery
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .Select(x => new FollowSuggestionCandidateModel
                {
                    AccountId = x.AccountId,
                    Username = x.Username,
                    FullName = x.FullName,
                    AvatarUrl = x.AvatarUrl,
                    IsContact = x.IsContact,
                    IsFollower = x.IsFollower,
                    MutualFollowCount = x.MutualConnectionCount
                })
                .ToListAsync();

            var pageLastContactedMap = await GetDirectConversationLastContactMapAsync(
                currentId,
                items.Select(x => x.AccountId).ToList());

            foreach (var item in items)
            {
                item.HasDirectConversation = pageLastContactedMap.ContainsKey(item.AccountId);
                item.LastContactedAt = pageLastContactedMap.GetValueOrDefault(item.AccountId);
            }

            return (items, totalItems);
        }

        public async Task<Dictionary<Guid, List<string>>> GetMutualFollowPreviewUsernamesAsync(
            Guid currentId,
            IEnumerable<Guid> targetIds,
            int perTargetLimit)
        {
            var normalizedTargetIds = targetIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (normalizedTargetIds.Count == 0 || perTargetLimit <= 0)
            {
                return new Dictionary<Guid, List<string>>();
            }

            var myFollowingIdsQuery = _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentId)
                .Select(f => f.FollowedId);

            var previewRows = await _context.Follows
                .AsNoTracking()
                .Where(f =>
                    normalizedTargetIds.Contains(f.FollowedId) &&
                    myFollowingIdsQuery.Contains(f.FollowerId) &&
                    f.Follower.Status == AccountStatusEnum.Active &&
                    SocialRoleRules.SocialEligibleRoleIds.Contains(f.Follower.RoleId))
                .Select(f => new
                {
                    TargetId = f.FollowedId,
                    PreviewUsername = f.Follower.Username,
                    f.CreatedAt
                })
                .OrderBy(x => x.TargetId)
                .ThenByDescending(x => x.CreatedAt)
                .ThenBy(x => x.PreviewUsername)
                .ToListAsync();

            return previewRows
                .GroupBy(x => x.TargetId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(x => x.PreviewUsername)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(perTargetLimit)
                        .ToList());
        }

        private async Task<List<GroupInviteAccountSearchModel>> GetRecentDirectChatInviteCandidatesAsync(
            Guid currentId,
            List<Guid> excludeIds,
            int takeCount)
        {
            var recentDirectChatRows = await _context.Conversations
                .AsNoTracking()
                .Where(c => !c.IsDeleted && !c.IsGroup)
                .Where(c => c.Members.Any(m => m.AccountId == currentId && !m.HasLeft))
                .Select(c => new
                {
                    OtherAccountId = c.Members
                        .Where(m => m.AccountId != currentId && !m.HasLeft)
                        .OrderBy(m => m.JoinedAt)
                        .ThenBy(m => m.AccountId)
                        .Select(m => m.AccountId)
                        .FirstOrDefault(),
                    LastMessageAt = c.Messages.Select(m => (DateTime?)m.SentAt).Max()
                })
                .Where(x => x.OtherAccountId != Guid.Empty && x.LastMessageAt.HasValue)
                .GroupBy(x => x.OtherAccountId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    LastMessageAt = g.Max(x => x.LastMessageAt)!.Value
                })
                .OrderByDescending(x => x.LastMessageAt)
                .Take(RecentDirectChatPrefetch)
                .ToListAsync();

            if (recentDirectChatRows.Count == 0)
            {
                return new List<GroupInviteAccountSearchModel>();
            }

            var recentDirectMessageMap = recentDirectChatRows.ToDictionary(x => x.AccountId, x => x.LastMessageAt);
            var recentAccountIds = recentDirectChatRows.Select(x => x.AccountId).ToList();

            var accountsQuery = GetSocialAccountsNoTrackingQuery()
                .Where(a => recentAccountIds.Contains(a.AccountId));

            var hiddenAccountIds = AccountBlockQueryHelper.CreateHiddenAccountIdsQuery(_context, currentId);
            accountsQuery = accountsQuery.Where(a => !hiddenAccountIds.Contains(a.AccountId));

            if (excludeIds.Count > 0)
            {
                accountsQuery = accountsQuery.Where(a => !excludeIds.Contains(a.AccountId));
            }

            var candidates = await accountsQuery
                .Select(a => new
                {
                    a.AccountId,
                    a.Username,
                    a.FullName,
                    a.AvatarUrl,
                    InvitePermission = a.Settings != null
                        ? a.Settings.GroupChatInvitePermission
                        : GroupChatInvitePermissionEnum.Anyone,
                    IsFollowing = _context.Follows.Any(f => f.FollowerId == currentId && f.FollowedId == a.AccountId),
                    IsFollower = _context.Follows.Any(f => f.FollowerId == a.AccountId && f.FollowedId == currentId)
                })
                .Where(x =>
                    x.InvitePermission == GroupChatInvitePermissionEnum.Anyone ||
                    (x.InvitePermission == GroupChatInvitePermissionEnum.FollowersOrFollowing && (x.IsFollowing || x.IsFollower)))
                .ToListAsync();

            if (candidates.Count == 0)
            {
                return new List<GroupInviteAccountSearchModel>();
            }

            var candidateIds = candidates.Select(x => x.AccountId).Distinct().ToList();

            var myGroupConversationIdsQuery = _context.ConversationMembers
                .AsNoTracking()
                .Where(cm => cm.AccountId == currentId && !cm.HasLeft && cm.Conversation.IsGroup && !cm.Conversation.IsDeleted)
                .Select(cm => cm.ConversationId);

            var mutualGroupRows = await _context.ConversationMembers
                .AsNoTracking()
                .Where(cm => candidateIds.Contains(cm.AccountId) && !cm.HasLeft && cm.Conversation.IsGroup && !cm.Conversation.IsDeleted)
                .Where(cm => myGroupConversationIdsQuery.Contains(cm.ConversationId))
                .GroupBy(cm => cm.AccountId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var mutualGroupCountMap = mutualGroupRows.ToDictionary(x => x.AccountId, x => x.Count);
            var nowUtc = DateTime.UtcNow;

            var results = candidates
                .Select(candidate =>
                {
                    var hasRecentChat = recentDirectMessageMap.TryGetValue(candidate.AccountId, out var recentChatAtValue);
                    DateTime? lastDirectMessageAt = hasRecentChat ? recentChatAtValue : null;

                    var followingScore = candidate.IsFollowing ? 500d : 0d;
                    var followerScore = candidate.IsFollower ? 420d : 0d;
                    var recentChatScore = ComputeRecentChatScore(lastDirectMessageAt, nowUtc);

                    mutualGroupCountMap.TryGetValue(candidate.AccountId, out var mutualGroupCount);
                    var mutualGroupScore = ComputeMutualGroupScore(mutualGroupCount);

                    var totalScore = followingScore + followerScore + recentChatScore + mutualGroupScore;

                    return new GroupInviteAccountSearchModel
                    {
                        AccountId = candidate.AccountId,
                        Username = candidate.Username,
                        FullName = candidate.FullName,
                        AvatarUrl = candidate.AvatarUrl,
                        IsFollowing = candidate.IsFollowing,
                        IsFollower = candidate.IsFollower,
                        MutualGroupCount = mutualGroupCount,
                        LastDirectMessageAt = lastDirectMessageAt,
                        MatchScore = 0d,
                        FollowingScore = followingScore,
                        FollowerScore = followerScore,
                        RecentChatScore = recentChatScore,
                        MutualGroupScore = mutualGroupScore,
                        TotalScore = totalScore
                    };
                })
                .OrderByDescending(x => x.LastDirectMessageAt)
                .ThenByDescending(x => x.FollowingScore)
                .ThenByDescending(x => x.FollowerScore)
                .ThenByDescending(x => x.MutualGroupScore)
                .ThenBy(x => x.Username)
                .Take(takeCount)
                .ToList();

            return results;
        }

        private static int NormalizeInviteSearchLimit(int limit)
        {
            if (limit <= 0)
            {
                return DefaultInviteSearchLimit;
            }

            return Math.Min(limit, MaxInviteSearchLimit);
        }

        private async Task<List<PostTagAccountSearchModel>> GetEmptyKeywordPostTagCandidatesAsync(
            Guid currentId,
            bool requireFollowerVisibility,
            List<Guid> excludeIds,
            int takeCount)
        {
            var recentDirectChatRows = await _context.Conversations
                .AsNoTracking()
                .Where(c => !c.IsDeleted && !c.IsGroup)
                .Where(c => c.Members.Any(m => m.AccountId == currentId && !m.HasLeft))
                .Select(c => new
                {
                    OtherAccountId = c.Members
                        .Where(m => m.AccountId != currentId && !m.HasLeft)
                        .OrderBy(m => m.JoinedAt)
                        .ThenBy(m => m.AccountId)
                        .Select(m => m.AccountId)
                        .FirstOrDefault(),
                    LastMessageAt = c.Messages.Select(m => (DateTime?)m.SentAt).Max()
                })
                .Where(x => x.OtherAccountId != Guid.Empty && x.LastMessageAt.HasValue)
                .GroupBy(x => x.OtherAccountId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    LastMessageAt = g.Max(x => x.LastMessageAt)!.Value
                })
                .OrderByDescending(x => x.LastMessageAt)
                .Take(EmptyKeywordPostTagPrefetch)
                .ToListAsync();

            var recentDirectMessageMap = recentDirectChatRows.ToDictionary(x => x.AccountId, x => x.LastMessageAt);
            var recentAccountIds = recentDirectChatRows.Select(x => x.AccountId).ToList();

            var candidatesQuery = GetSocialAccountsNoTrackingQuery()
                .Where(a => a.AccountId != currentId);

            if (excludeIds.Count > 0)
            {
                candidatesQuery = candidatesQuery.Where(a => !excludeIds.Contains(a.AccountId));
            }

            var candidates = await candidatesQuery
                .Select(a => new PreliminaryPostTagSearchCandidate
                {
                    AccountId = a.AccountId,
                    Username = a.Username,
                    FullName = a.FullName,
                    AvatarUrl = a.AvatarUrl,
                    TagPermission = a.Settings != null
                        ? a.Settings.TagPermission
                        : TagPermissionEnum.Anyone,
                    IsFollowing = _context.Follows.Any(f => f.FollowerId == currentId && f.FollowedId == a.AccountId),
                    IsFollower = _context.Follows.Any(f => f.FollowerId == a.AccountId && f.FollowedId == currentId),
                    HasRecentChat = recentAccountIds.Contains(a.AccountId)
                })
                .Where(x => x.TagPermission != TagPermissionEnum.NoOne)
                .Where(x => !requireFollowerVisibility || x.IsFollower)
                .OrderByDescending(x => x.IsFollowing)
                .ThenByDescending(x => x.IsFollower)
                .ThenByDescending(x => x.HasRecentChat)
                .ThenBy(x => x.Username)
                .Take(Math.Max(takeCount * 10, 80))
                .ToListAsync();

            if (candidates.Count == 0)
            {
                return new List<PostTagAccountSearchModel>();
            }

            var nowUtc = DateTime.UtcNow;

            var results = candidates
                .Select(candidate =>
                {
                    recentDirectMessageMap.TryGetValue(candidate.AccountId, out var lastContactedAt);
                    var recentChatScore = ComputeRecentChatScore(lastContactedAt, nowUtc);
                    var followingScore = candidate.IsFollowing ? 520d : 0d;
                    var followerScore = candidate.IsFollower ? 200d : 0d;
                    var totalScore = followingScore + followerScore + recentChatScore;

                    return new PostTagAccountSearchModel
                    {
                        AccountId = candidate.AccountId,
                        Username = candidate.Username,
                        FullName = candidate.FullName,
                        AvatarUrl = candidate.AvatarUrl,
                        IsFollowing = candidate.IsFollowing,
                        IsFollower = candidate.IsFollower,
                        LastContactedAt = lastContactedAt,
                        MatchScore = 0d,
                        FollowingScore = followingScore,
                        FollowerScore = followerScore,
                        RecentChatScore = recentChatScore,
                        TotalScore = totalScore
                    };
                })
                .OrderByDescending(x => x.TotalScore)
                .ThenByDescending(x => x.IsFollowing)
                .ThenByDescending(x => x.IsFollower)
                .ThenByDescending(x => x.LastContactedAt)
                .ThenBy(x => x.Username)
                .Take(takeCount)
                .ToList();

            return results;
        }

        private static int NormalizePostShareSearchLimit(int limit)
        {
            if (limit <= 0)
            {
                return DefaultPostShareSearchLimit;
            }

            return Math.Min(limit, MaxPostShareSearchLimit);
        }

        private static int NormalizeSidebarSearchLimit(int limit)
        {
            if (limit <= 0)
            {
                return DefaultSidebarSearchLimit;
            }

            return Math.Min(limit, MaxSidebarSearchLimit);
        }

        private static int NormalizeSidebarSearchPrefetchLimit(int safeLimit, int keywordLength)
        {
            var multiplier = SidebarSearchPrefetchMultiplier;
            var minPrefetch = MinSidebarSearchPrefetch;
            var maxPrefetch = MaxSidebarSearchPrefetch;

            if (keywordLength <= 1)
            {
                multiplier = ShortSidebarSearchPrefetchMultiplier;
                minPrefetch = MinShortSidebarSearchPrefetch;
                maxPrefetch = MaxShortSidebarSearchPrefetch;
            }

            return Math.Min(
                Math.Max(safeLimit * multiplier, minPrefetch),
                maxPrefetch);
        }

        private static int NormalizePostTagSearchLimit(int limit)
        {
            if (limit <= 0)
            {
                return DefaultPostTagSearchLimit;
            }

            return Math.Min(limit, MaxPostTagSearchLimit);
        }

        private static double ComputePostShareAccountMatchScore(PreliminaryPostShareAccountCandidate candidate)
        {
            double usernameScore;
            if (candidate.UsernameStartsWith)
            {
                usernameScore = 3200d;
            }
            else if (candidate.UsernameContains)
            {
                usernameScore = 2200d;
            }
            else if (candidate.UsernameSimilarity >= FuzzySimilarityThreshold)
            {
                usernameScore = 1000d + ((candidate.UsernameSimilarity - FuzzySimilarityThreshold) * 1200d);
            }
            else
            {
                usernameScore = 0d;
            }

            double fullNameScore;
            if (candidate.FullNameStartsWith)
            {
                fullNameScore = 2800d;
            }
            else if (candidate.FullNameContains)
            {
                fullNameScore = 1900d;
            }
            else if (candidate.FullNameSimilarity >= FuzzySimilarityThreshold)
            {
                fullNameScore = 900d + ((candidate.FullNameSimilarity - FuzzySimilarityThreshold) * 1000d);
            }
            else
            {
                fullNameScore = 0d;
            }

            return Math.Max(usernameScore, fullNameScore);
        }

        private static double ComputeSidebarMatchScore(PreliminarySidebarSearchCandidate candidate)
        {
            double usernameScore;
            if (candidate.UsernameExact)
            {
                usernameScore = 5200d;
            }
            else if (candidate.UsernameStartsWith)
            {
                usernameScore = 4100d;
            }
            else if (candidate.UsernameContains)
            {
                usernameScore = 2500d;
            }
            else if (candidate.UsernameSimilarity >= SidebarSearchRankingHelper.FuzzySimilarityThreshold)
            {
                usernameScore = 700d + ((candidate.UsernameSimilarity - SidebarSearchRankingHelper.FuzzySimilarityThreshold) * 1000d);
            }
            else
            {
                usernameScore = 0d;
            }

            double fullNameScore;
            if (candidate.FullNameStartsWith)
            {
                fullNameScore = 3200d;
            }
            else if (candidate.FullNameWordStartsWith)
            {
                fullNameScore = 2800d;
            }
            else if (candidate.FullNameContains)
            {
                fullNameScore = 1900d;
            }
            else if (candidate.FullNameSimilarity >= SidebarSearchRankingHelper.FuzzySimilarityThreshold)
            {
                fullNameScore = 550d + ((candidate.FullNameSimilarity - SidebarSearchRankingHelper.FuzzySimilarityThreshold) * 900d);
            }
            else
            {
                fullNameScore = 0d;
            }

            return Math.Max(usernameScore, fullNameScore);
        }

        private static bool IsSidebarCandidateEligible(
            PreliminarySidebarSearchCandidate candidate,
            int keywordLength)
        {
            if (IsSidebarStrongMatch(candidate))
            {
                return true;
            }

            return IsSidebarFuzzyMatchEligible(candidate.Username, keywordLength, candidate.UsernameSimilarity) ||
                IsSidebarFuzzyMatchEligible(candidate.FullName, keywordLength, candidate.FullNameSimilarity);
        }

        private static bool IsSidebarStrongMatch(PreliminarySidebarSearchCandidate candidate)
        {
            return SidebarSearchRankingHelper.IsStrongMatch(
                candidate.UsernameExact,
                candidate.UsernameStartsWith,
                candidate.FullNameStartsWith,
                candidate.FullNameWordStartsWith,
                candidate.UsernameContains,
                candidate.FullNameContains);
        }

        private static bool IsSidebarFuzzyMatchEligible(
            string? candidateValue,
            int keywordLength,
            double similarity)
        {
            return SidebarSearchRankingHelper.IsFuzzyMatchEligible(
                candidateValue,
                keywordLength,
                similarity);
        }

        private static double ComputeMatchScore(PreliminaryInviteSearchCandidate candidate)
        {
            double usernameScore;
            if (candidate.UsernameStartsWith)
            {
                usernameScore = 4000d;
            }
            else if (candidate.UsernameContains)
            {
                usernameScore = 2600d;
            }
            else if (candidate.UsernameSimilarity >= FuzzySimilarityThreshold)
            {
                usernameScore = 900d + ((candidate.UsernameSimilarity - FuzzySimilarityThreshold) * 1200d);
            }
            else
            {
                usernameScore = 0d;
            }

            double fullNameScore;
            if (candidate.FullNameStartsWith)
            {
                fullNameScore = 3600d;
            }
            else if (candidate.FullNameContains)
            {
                fullNameScore = 2200d;
            }
            else if (candidate.FullNameSimilarity >= FuzzySimilarityThreshold)
            {
                fullNameScore = 800d + ((candidate.FullNameSimilarity - FuzzySimilarityThreshold) * 900d);
            }
            else
            {
                fullNameScore = 0d;
            }

            return Math.Max(usernameScore, fullNameScore);
        }

        private static double ComputePostTagMatchScore(PreliminaryPostTagSearchCandidate candidate)
        {
            double usernameScore;
            if (candidate.UsernameStartsWith)
            {
                usernameScore = 3800d;
            }
            else if (candidate.UsernameContains)
            {
                usernameScore = 2500d;
            }
            else if (candidate.UsernameSimilarity >= FuzzySimilarityThreshold)
            {
                usernameScore = 900d + ((candidate.UsernameSimilarity - FuzzySimilarityThreshold) * 1200d);
            }
            else
            {
                usernameScore = 0d;
            }

            double fullNameScore;
            if (candidate.FullNameStartsWith)
            {
                fullNameScore = 3400d;
            }
            else if (candidate.FullNameContains)
            {
                fullNameScore = 2100d;
            }
            else if (candidate.FullNameSimilarity >= FuzzySimilarityThreshold)
            {
                fullNameScore = 800d + ((candidate.FullNameSimilarity - FuzzySimilarityThreshold) * 900d);
            }
            else
            {
                fullNameScore = 0d;
            }

            return Math.Max(usernameScore, fullNameScore);
        }

        private static double ComputeRecentChatScore(DateTime? lastDirectMessageAt, DateTime nowUtc)
        {
            if (!lastDirectMessageAt.HasValue)
            {
                return 0d;
            }

            if (lastDirectMessageAt.Value >= nowUtc.AddDays(-7))
            {
                return 700d;
            }

            if (lastDirectMessageAt.Value >= nowUtc.AddDays(-30))
            {
                return 550d;
            }

            if (lastDirectMessageAt.Value >= nowUtc.AddDays(-90))
            {
                return 350d;
            }

            return 150d;
        }

        private async Task<Dictionary<Guid, DateTime?>> GetDirectConversationLastContactMapAsync(
            Guid currentId,
            List<Guid> candidateIds)
        {
            if (candidateIds.Count == 0)
            {
                return new Dictionary<Guid, DateTime?>();
            }

            var directChatRows = await _context.Conversations
                .AsNoTracking()
                .Where(c => !c.IsDeleted && !c.IsGroup)
                .Where(c => c.Members.Any(m => m.AccountId == currentId && !m.HasLeft))
                .Where(c => c.Members.Any(m => candidateIds.Contains(m.AccountId) && !m.HasLeft))
                .Select(c => new
                {
                    OtherAccountId = c.Members
                        .Where(m => m.AccountId != currentId && candidateIds.Contains(m.AccountId) && !m.HasLeft)
                        .OrderBy(m => m.JoinedAt)
                        .ThenBy(m => m.AccountId)
                        .Select(m => m.AccountId)
                        .FirstOrDefault(),
                    LastMessageAt = c.Messages.Select(m => (DateTime?)m.SentAt).Max()
                })
                .Where(x => x.OtherAccountId != Guid.Empty)
                .ToListAsync();

            return directChatRows
                .GroupBy(x => x.OtherAccountId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.LastMessageAt));
        }

        private static double ComputeSidebarHistoryScore(DateTime? lastSearchedAt, DateTime nowUtc)
        {
            if (!lastSearchedAt.HasValue)
            {
                return 0d;
            }

            if (lastSearchedAt.Value >= nowUtc.AddDays(-7))
            {
                return 320d;
            }

            if (lastSearchedAt.Value >= nowUtc.AddDays(-30))
            {
                return 220d;
            }

            if (lastSearchedAt.Value >= nowUtc.AddDays(-90))
            {
                return 120d;
            }

            return 60d;
        }

        private static double ComputeMutualGroupScore(int mutualGroupCount)
        {
            return Math.Min(mutualGroupCount * 120d, 480d);
        }

        private static long CreateFollowSuggestionJitterSeed(Guid currentId, bool prioritizeDiscovery, DateTime nowUtc)
        {
            var bytes = currentId.ToByteArray();
            var primary = BitConverter.ToInt64(bytes, 0);
            var secondary = BitConverter.ToInt64(bytes, 8);
            var dayBucket = nowUtc.Date.Ticks;
            var surfaceSeed = prioritizeDiscovery ? 0x484F4D45L : 0x50414745L;

            return primary ^ secondary ^ dayBucket ^ surfaceSeed;
        }

        private static long ComputeInMemoryFollowSuggestionJitterHash(string? username, long seed)
        {
            var normalizedUsername = (username ?? string.Empty).Trim();

            unchecked
            {
                const ulong fnvOffsetBasis = 14695981039346656037UL;
                const ulong fnvPrime = 1099511628211UL;

                var hash = fnvOffsetBasis ^ (ulong)seed;
                for (var i = 0; i < normalizedUsername.Length; i++)
                {
                    hash ^= normalizedUsername[i];
                    hash *= fnvPrime;
                }

                hash ^= (ulong)normalizedUsername.Length;
                hash *= fnvPrime;

                return (long)hash;
            }
        }

        private bool ShouldUseInMemorySuggestionFallback()
        {
            var providerName = _context.Database.ProviderName ?? string.Empty;
            return providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
        }

        private static IOrderedQueryable<FollowSuggestionCandidateProjection> ApplyFollowSuggestionOrdering(
            IQueryable<FollowSuggestionCandidateProjection> rankedCandidatesQuery,
            bool prioritizeDiscovery,
            int jitterBucketCount)
        {
            return prioritizeDiscovery
                ? rankedCandidatesQuery
                    .OrderByDescending(x => x.IsContact)
                    .ThenByDescending(x => x.IsFollower)
                    .ThenByDescending(x => x.MutualConnectionCount > 0)
                    .ThenBy(x => Math.Abs(x.RawJitterHash % jitterBucketCount))
                    .ThenByDescending(x => x.MutualConnectionCount)
                    .ThenByDescending(x => x.FollowersCount)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Username)
                : rankedCandidatesQuery
                    .OrderByDescending(x => x.IsContact)
                    .ThenByDescending(x => x.IsFollower)
                    .ThenByDescending(x => x.MutualConnectionCount)
                    .ThenByDescending(x => x.FollowersCount)
                    .ThenBy(x => Math.Abs(x.RawJitterHash % jitterBucketCount))
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Username);
        }

        private static IOrderedEnumerable<FollowSuggestionCandidateProjection> ApplyFollowSuggestionOrdering(
            IEnumerable<FollowSuggestionCandidateProjection> rankedCandidates,
            bool prioritizeDiscovery,
            int jitterBucketCount)
        {
            return prioritizeDiscovery
                ? rankedCandidates
                    .OrderByDescending(x => x.IsContact)
                    .ThenByDescending(x => x.IsFollower)
                    .ThenByDescending(x => x.MutualConnectionCount > 0)
                    .ThenBy(x => Math.Abs(x.RawJitterHash % jitterBucketCount))
                    .ThenByDescending(x => x.MutualConnectionCount)
                    .ThenByDescending(x => x.FollowersCount)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Username)
                : rankedCandidates
                    .OrderByDescending(x => x.IsContact)
                    .ThenByDescending(x => x.IsFollower)
                    .ThenByDescending(x => x.MutualConnectionCount)
                    .ThenByDescending(x => x.FollowersCount)
                    .ThenBy(x => Math.Abs(x.RawJitterHash % jitterBucketCount))
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Username);
        }

        private sealed class FollowSuggestionCandidateProjection
        {
            public Guid AccountId { get; set; }
            public string Username { get; set; } = null!;
            public string FullName { get; set; } = null!;
            public string? AvatarUrl { get; set; }
            public DateTime CreatedAt { get; set; }
            public bool IsContact { get; set; }
            public bool IsFollower { get; set; }
            public int MutualConnectionCount { get; set; }
            public int FollowersCount { get; set; }
            public long RawJitterHash { get; set; }
        }

        private sealed class PreliminaryInviteSearchCandidate
        {
            public Guid AccountId { get; set; }
            public string Username { get; set; } = null!;
            public string FullName { get; set; } = null!;
            public string? AvatarUrl { get; set; }
            public GroupChatInvitePermissionEnum InvitePermission { get; set; }
            public bool IsFollowing { get; set; }
            public bool IsFollower { get; set; }
            public bool UsernameStartsWith { get; set; }
            public bool FullNameStartsWith { get; set; }
            public bool UsernameContains { get; set; }
            public bool FullNameContains { get; set; }
            public double UsernameSimilarity { get; set; }
            public double FullNameSimilarity { get; set; }
        }

        private sealed class PreliminaryPostShareAccountCandidate
        {
            public Guid AccountId { get; set; }
            public string Username { get; set; } = null!;
            public string FullName { get; set; } = null!;
            public string? AvatarUrl { get; set; }
            public bool UsernameStartsWith { get; set; }
            public bool FullNameStartsWith { get; set; }
            public bool UsernameContains { get; set; }
            public bool FullNameContains { get; set; }
            public double UsernameSimilarity { get; set; }
            public double FullNameSimilarity { get; set; }
        }

        private sealed class PreliminarySidebarSearchCandidate
        {
            public Guid AccountId { get; set; }
            public string Username { get; set; } = null!;
            public string FullName { get; set; } = null!;
            public string? AvatarUrl { get; set; }
            public bool IsFollowing { get; set; }
            public bool IsFollower { get; set; }
            public bool HasSearchHistory { get; set; }
            public bool UsernameExact { get; set; }
            public bool UsernameStartsWith { get; set; }
            public bool FullNameStartsWith { get; set; }
            public bool FullNameWordStartsWith { get; set; }
            public bool UsernameContains { get; set; }
            public bool FullNameContains { get; set; }
            public double UsernameSimilarity { get; set; }
            public double FullNameSimilarity { get; set; }
        }

        private sealed class PreliminaryPostTagSearchCandidate
        {
            public Guid AccountId { get; set; }
            public string Username { get; set; } = null!;
            public string FullName { get; set; } = null!;
            public string? AvatarUrl { get; set; }
            public TagPermissionEnum TagPermission { get; set; }
            public bool IsFollowing { get; set; }
            public bool IsFollower { get; set; }
            public bool IsVisibilityFollower { get; set; }
            public bool HasRecentChat { get; set; }
            public bool UsernameStartsWith { get; set; }
            public bool FullNameStartsWith { get; set; }
            public bool UsernameContains { get; set; }
            public bool FullNameContains { get; set; }
            public double UsernameSimilarity { get; set; }
            public double FullNameSimilarity { get; set; }
        }
    }
}
