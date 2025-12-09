using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.AccountDTOs
{
    public class AccountUpdateRequest
    {
        public int? RoleId { get; set; }
        public bool? Status { get; set; } 
        public bool? IsEmailVerified { get; set; }
    }
}
