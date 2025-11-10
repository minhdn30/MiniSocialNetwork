using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Entities
{
    public class Role
    {
        public int RoleId { get; set; }
        [Required, MaxLength(15)]
        public string RoleName { get; set; } = null!;
        public virtual ICollection<Account>? Accounts { get; set; }
    }
}
