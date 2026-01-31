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
            return await _context.Accounts.AnyAsync(a => a.AccountId == accountId);
        }
        public async Task AddAccount(Account account)
        {
            await _context.Accounts.AddAsync(account);
            await _context.SaveChangesAsync();
        }
        public async Task<Account?> GetAccountById(Guid accountId)
        {
            return await _context.Accounts.Include(a => a.Role).FirstOrDefaultAsync(a => a.AccountId == accountId);
        }
        public async Task<Account?> GetAccountProfileById(Guid accountId)
        {
            return await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId && a.Status == true);
        }
        public async Task<Account?> GetAccountByEmail(string email)
        {
            return await _context.Accounts.Include(a => a.Role).FirstOrDefaultAsync(a => a.Email.ToLower() == email.ToLower());
        }
        public async Task UpdateAccount(Account account)
        {
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();
        }
        public async Task<Account?> GetAccountByUsername(string username)
        {
            return await _context.Accounts.Include(a => a.Role).FirstOrDefaultAsync(a => a.Username.ToLower() == username.ToLower());
        }
        public async Task<Account?> GetByRefreshToken(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            return await _context.Accounts
                .FirstOrDefaultAsync(a => a.RefreshToken == refreshToken);
        }
        //search and filter accounts (admin)
        public async Task<(List<Account> Items, int TotalItems)> GetAccountsAsync(Guid? id, string? username, string? email,
            string? fullname, string? phone, int? roleId, bool? gender, bool? status, bool? isEmailVerified, int page, int pageSize)
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
                var keywords = fullname.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in keywords)
                {
                    var k = word.ToLower();
                    query = query.Where(a => a.FullName.ToLower().Contains(k));
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
                .Where(a => a.AccountId == targetId)
                .Select(a => new
                {
                    Account = new AccountBasicInfoModel
                    {
                        AccountId = a.AccountId,
                        Username = a.Username,
                        FullName = a.FullName,
                        AvatarUrl = a.AvatarUrl
                    },

                    PostCount = a.Posts.Count(p => !p.IsDeleted),
                    FollowerCount = a.Followers.Count(),
                    FollowingCount = a.Followings.Count(),

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


    }
}
