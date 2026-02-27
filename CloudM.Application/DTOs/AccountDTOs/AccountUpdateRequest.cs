using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.AccountDTOs
{
    public class AccountUpdateRequest
    {
        public int? RoleId { get; set; }
        public AccountStatusEnum? Status { get; set; } 
    }
}
