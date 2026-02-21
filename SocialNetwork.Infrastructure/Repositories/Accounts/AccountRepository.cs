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
using System.Xml.Linq;

namespace SocialNetwork.Infrastructure.Repositories.Accounts
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

        private readonly AppDbContext _context;
        public AccountRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<bool> IsUsernameExist (string username)
        {
            var normalizedUsername = (username ?? string.Empty).Trim().ToLower();
            return await _context.Accounts.AnyAsync(a => a.Username.ToLower() == normalizedUsername);
        }
        public async Task<bool> IsEmailExist(string email)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLower();
            return await _context.Accounts.AnyAsync(a => a.Email.ToLower() == normalizedEmail);
        }
        public async Task<bool> IsAccountIdExist(Guid accountId)
        {
            return await _context.Accounts.AnyAsync(a => a.AccountId == accountId && a.Status == AccountStatusEnum.Active);
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
            return await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Settings)
                .FirstOrDefaultAsync(a => a.Email.ToLower() == email.ToLower());
        }
        public Task UpdateAccount(Account account)
        {
            _context.Accounts.Update(account);
            return Task.CompletedTask;
        }
        public async Task<Account?> GetAccountByUsername(string username)
        {
            return await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Settings)
                .FirstOrDefaultAsync(a => a.Username.ToLower() == username.ToLower());
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
            var data = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.AccountId == targetId && a.Status == AccountStatusEnum.Active)
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

                    PostCount = a.Posts.Count(p => !p.IsDeleted),
                    FollowerCount = a.Followers.Count(f => f.Follower.Status == AccountStatusEnum.Active),
                    FollowingCount = a.Followings.Count(f => f.Followed.Status == AccountStatusEnum.Active),

                    IsCurrentUser = currentId.HasValue && a.AccountId == currentId.Value,
                    IsFollowedByCurrentUser =
                        currentId.HasValue && a.Followers.Any(f => f.FollowerId == currentId.Value),

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
                RecentPosts = data.RecentMedias
            };
        }


        public async Task<ProfileInfoModel?> GetProfileInfoAsync(Guid targetId, Guid? currentId)
        {
            var data = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.AccountId == targetId && a.Status == AccountStatusEnum.Active)
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
                    PostCount = a.Posts.Count(p => !p.IsDeleted),
                    FollowerCount = a.Followers.Count(f => f.Follower.Status == AccountStatusEnum.Active),
                    FollowingCount = a.Followings.Count(f => f.Followed.Status == AccountStatusEnum.Active),
                    IsFollowedByCurrentUser = currentId.HasValue && a.Followers.Any(f => f.FollowerId == currentId.Value),
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

                // virtual defaults
                PhonePrivacy = s?.PhonePrivacy ?? AccountPrivacyEnum.Private,
                AddressPrivacy = s?.AddressPrivacy ?? AccountPrivacyEnum.Private,
                DefaultPostPrivacy = s?.DefaultPostPrivacy ?? PostPrivacyEnum.Public,
                FollowerPrivacy = s?.FollowerPrivacy ?? AccountPrivacyEnum.Public,
                FollowingPrivacy = s?.FollowingPrivacy ?? AccountPrivacyEnum.Public,
                GroupChatInvitePermission = s?.GroupChatInvitePermission ?? GroupChatInvitePermissionEnum.Anyone,
            };
        }

        public async Task<ProfileInfoModel?> GetProfileInfoByUsernameAsync(string username, Guid? currentId)
        {
            var data = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.Username.ToLower() == username.ToLower() && a.Status == AccountStatusEnum.Active)
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
                    PostCount = a.Posts.Count(p => !p.IsDeleted),
                    FollowerCount = a.Followers.Count(f => f.Follower.Status == AccountStatusEnum.Active),
                    FollowingCount = a.Followings.Count(f => f.Followed.Status == AccountStatusEnum.Active),
                    IsFollowedByCurrentUser = currentId.HasValue && a.Followers.Any(f => f.FollowerId == currentId.Value),
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

                // virtual defaults
                PhonePrivacy = s?.PhonePrivacy ?? AccountPrivacyEnum.Private,
                AddressPrivacy = s?.AddressPrivacy ?? AccountPrivacyEnum.Private,
                DefaultPostPrivacy = s?.DefaultPostPrivacy ?? PostPrivacyEnum.Public,
                FollowerPrivacy = s?.FollowerPrivacy ?? AccountPrivacyEnum.Public,
                FollowingPrivacy = s?.FollowingPrivacy ?? AccountPrivacyEnum.Public,
                GroupChatInvitePermission = s?.GroupChatInvitePermission ?? GroupChatInvitePermissionEnum.Anyone
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

            var query = _context.Accounts
                .AsNoTracking()
                .Where(a => a.Status == AccountStatusEnum.Active && a.AccountId != currentId);

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
                        .Where(m => m.AccountId != currentId && candidateIds.Contains(m.AccountId))
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

        public async Task<List<Account>> GetAccountsByIds(IEnumerable<Guid> accountIds)
        {
            return await _context.Accounts
                .Include(a => a.Settings)
                .Where(a => accountIds.Contains(a.AccountId))
                .ToListAsync();
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

            var accountsQuery = _context.Accounts
                .AsNoTracking()
                .Where(a => recentAccountIds.Contains(a.AccountId) && a.Status == AccountStatusEnum.Active);

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

        private static double ComputeMutualGroupScore(int mutualGroupCount)
        {
            return Math.Min(mutualGroupCount * 120d, 480d);
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
    }
}
