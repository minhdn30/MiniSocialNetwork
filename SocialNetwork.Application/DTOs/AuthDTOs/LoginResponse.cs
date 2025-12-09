using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.AuthDTOs
{
    public class LoginResponse
    {
        public Guid AccountId { get; set; }
        public string Fullname { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }
    }
}
