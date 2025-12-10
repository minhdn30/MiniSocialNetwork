using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.AccountDTOs
{
    public class AccountPostDetailResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string FullName { get; set; } = null!;
    }
}
