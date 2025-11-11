using SocialNetwork.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(Account account);
        string? ValidateToken(string token);
    }
}
