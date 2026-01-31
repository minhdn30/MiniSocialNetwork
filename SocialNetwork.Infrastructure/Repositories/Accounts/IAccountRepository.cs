using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.Accounts
{
    public interface IAccountRepository
    {
        Task<bool> IsUsernameExist(string username);
        Task<bool> IsEmailExist(string email);
        Task<bool> IsAccountIdExist(Guid accountId);
        Task AddAccount(Account account);
        Task<Account?> GetAccountById(Guid accountId);
        Task<Account?> GetAccountProfileById(Guid accountId);
        Task<Account?> GetAccountByEmail(string email);
        Task UpdateAccount(Account account);
        Task<Account?> GetAccountByUsername(string username);
        Task<Account?> GetByRefreshToken(string refreshToken);
        Task<(List<Account> Items, int TotalItems)> GetAccountsAsync(Guid? id, string? username, string? email,
            string? fullname, string? phone, int? roleId, bool? gender, bool? status, bool? isEmailVerified, int page, int pageSize);
        Task<AccountProfilePreviewModel?> GetProfilePreviewAsync(Guid targetId, Guid? currentId);
    }
}
