using CloudM.Domain.Entities;
using CloudM.Domain.Enums;

namespace CloudM.Domain.Helpers
{
    public static class SocialRoleRules
    {
        private static readonly HashSet<int> SocialEligibleRoleIdSet = new()
        {
            (int)RoleEnum.User
        };

        public static readonly int[] SocialEligibleRoleIds =
        {
            (int)RoleEnum.User
        };

        public static bool IsSocialEligibleRole(int roleId)
        {
            return SocialEligibleRoleIdSet.Contains(roleId);
        }

        public static bool IsSocialEligible(Account? account)
        {
            return account != null &&
                   account.Status == AccountStatusEnum.Active &&
                   IsSocialEligibleRole(account.RoleId);
        }
    }
}
