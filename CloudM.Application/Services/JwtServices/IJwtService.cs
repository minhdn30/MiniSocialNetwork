using CloudM.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.JwtServices
{
    public interface IJwtService
    {
        string GenerateToken(Account account);
        string? ValidateToken(string token);
    }
}
