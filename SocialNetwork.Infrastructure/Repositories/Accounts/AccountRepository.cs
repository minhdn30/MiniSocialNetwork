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
        private readonly AppDbContext _context;
        public AccountRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<bool> IsUsernameExist (string username)
        {
            return await _context.Accounts.AnyAsync(a => a.Username == username);
        }
        public async Task<bool> IsEmailExist(string email)
        {
            return await _context.Accounts.AnyAsync(a => a.Email == email);
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
                .FirstOrDefaultAsync(a => a.RefreshToken == refreshToken);
        }
        // search and filter accounts (admin)
        public async Task<(List<Account> Items, int TotalItems)> GetAccountsAsync(Guid? id, string? username, string? email,
            string? fullname, string? phone, int? roleId, bool? gender, AccountStatusEnum? status, bool? isEmailVerified, int page, int pageSize)
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
            if (isEmailVerified.HasValue)
            {
                query = query.Where(a => a.IsEmailVerified == isEmailVerified.Value);
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
                FollowingPrivacy = s?.FollowingPrivacy ?? AccountPrivacyEnum.Public
            };
        }

        public async Task<List<Account>> GetAccountsByIds(IEnumerable<Guid> accountIds)
        {
            return await _context.Accounts
                .Where(a => accountIds.Contains(a.AccountId))
                .ToListAsync();
        }
    }
}
