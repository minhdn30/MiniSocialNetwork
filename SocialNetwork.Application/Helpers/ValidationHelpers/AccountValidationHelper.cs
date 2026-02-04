using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Helpers.ValidationHelpers
{
    public static class AccountValidationHelper
    {
        public static async Task CheckAccountStatusAsync(IAccountRepository accountRepository, Guid accountId, string action = "perform this action")
        {
            var account = await accountRepository.GetAccountById(accountId);
            if (account == null)
                throw new NotFoundException($"Account with ID {accountId} does not exist.");

            if (account.Status != AccountStatusEnum.Active)
            {
                if (account.Status == AccountStatusEnum.Inactive)
                {
                    throw new ForbiddenException($"You must reactivate your account to {action}.");
                }
                else
                {
                    throw new ForbiddenException($"Your account is {account.Status}. You cannot {action}.");
                }
            }
        }

        public static void CheckAccountStatus(SocialNetwork.Domain.Entities.Account? account, string action = "perform this action")
        {
            if (account == null)
                throw new NotFoundException("Account not found.");

            if (account.Status != AccountStatusEnum.Active)
            {
                if (account.Status == AccountStatusEnum.Inactive)
                {
                    throw new ForbiddenException($"You must reactivate your account to {action}.");
                }
                else
                {
                    throw new ForbiddenException($"Your account is {account.Status}. You cannot {action}.");
                }
            }
        }
    }
}
