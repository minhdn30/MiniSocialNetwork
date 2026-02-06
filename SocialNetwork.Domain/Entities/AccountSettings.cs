using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Entities
{
    public class AccountSettings
    {
        [Key]
        [ForeignKey("Account")]
        public Guid AccountId { get; set; }

        public AccountPrivacyEnum EmailPrivacy { get; set; } = AccountPrivacyEnum.Private;
        public AccountPrivacyEnum PhonePrivacy { get; set; } = AccountPrivacyEnum.Private;
        public AccountPrivacyEnum AddressPrivacy { get; set; } = AccountPrivacyEnum.Private;
        
        public PostPrivacyEnum DefaultPostPrivacy { get; set; } = PostPrivacyEnum.Public;
        
        public AccountPrivacyEnum FollowerPrivacy { get; set; } = AccountPrivacyEnum.Public;
        public AccountPrivacyEnum FollowingPrivacy { get; set; } = AccountPrivacyEnum.Public;

        public virtual Account Account { get; set; } = null!;
    }
}
