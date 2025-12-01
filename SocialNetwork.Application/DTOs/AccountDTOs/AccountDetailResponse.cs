using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.AccountDTOs
{
    public class AccountDetailResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
        public bool? Gender { get; set; }
        public string? Address { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; } = null!;
        public bool Status { get; set; } = true;
        public DateTime CreatedAt { get; set; } 
        public DateTime? UpdatedAt { get; set; }
        public bool IsEmailVerified { get; set; } 
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public DateTime? LastActiveAt { get; set; }
    }
}
