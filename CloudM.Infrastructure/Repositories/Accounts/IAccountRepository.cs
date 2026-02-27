using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.Accounts
{
    public interface IAccountRepository
    {
        Task<bool> IsUsernameExist(string username);
        Task<bool> IsEmailExist(string email);
        Task<bool> IsAccountIdExist(Guid accountId);
        Task AddAccount(Account account);
        Task UpdateAccount(Account account);
        Task<Account?> GetAccountById(Guid accountId);
        Task<Account?> GetAccountByEmail(string email);
        Task<Account?> GetAccountByUsername(string username);
        Task<Account?> GetByRefreshToken(string refreshToken);
        Task<(List<Account> Items, int TotalItems)> GetAccountsAsync(Guid? id, string? username, string? email,
            string? fullname, string? phone, int? roleId, bool? gender, AccountStatusEnum? status, int page, int pageSize);
        Task<AccountProfilePreviewModel?> GetProfilePreviewAsync(Guid targetId, Guid? currentId);
        Task<ProfileInfoModel?> GetProfileInfoAsync(Guid targetId, Guid? currentId);
        Task<ProfileInfoModel?> GetProfileInfoByUsernameAsync(string username, Guid? currentId);
        Task<List<Account>> GetAccountsByIds(IEnumerable<Guid> accountIds);
        Task<List<GroupInviteAccountSearchModel>> SearchAccountsForGroupInviteAsync(
            Guid currentId,
            string keyword,
            IEnumerable<Guid>? excludeAccountIds,
            int limit = 10);
    }
}
