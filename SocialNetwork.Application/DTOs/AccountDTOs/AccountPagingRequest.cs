using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.AccountDTOs
{
    public class AccountPagingRequest
    {
        public Guid? Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Fullname { get; set; }
        public string? Phone { get; set; }
        public int? RoleId { get; set; }
        public bool? Gender { get; set; }
        public AccountStatusEnum? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
