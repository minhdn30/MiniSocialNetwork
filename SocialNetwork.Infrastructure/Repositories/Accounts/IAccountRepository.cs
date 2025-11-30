using SocialNetwork.Domain.Entities;
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
        Task AddAccount(Account account);
        Task<Account?> GetAccountById(Guid accountId);
        Task<Account?> GetAccountByEmail(string email);
        Task UpdateAccount(Account account);
        Task<Account?> GetAccountByUsername(string username);
        Task<Account?> GetByRefreshToken(string refreshToken);
    }
}
