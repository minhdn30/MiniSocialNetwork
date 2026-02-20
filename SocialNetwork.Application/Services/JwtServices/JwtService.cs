using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.JwtServices
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config)
        {
            _config = config;
        }
        public string GenerateToken(Account account)
        {
            var jwtSettings = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var accessTokenMinutes = jwtSettings.GetValue<int?>("AccessTokenMinutes") ?? 5;
            if (accessTokenMinutes <= 0)
            {
                accessTokenMinutes = 5;
            }

            var roleName = account.Role?.RoleName;
            if (string.IsNullOrWhiteSpace(roleName))
            {
                roleName = Enum.GetName(typeof(RoleEnum), account.RoleId) ?? RoleEnum.User.ToString();
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.AccountId.ToString()),     // account id
                new Claim("AccountId", account.AccountId.ToString()),                   // legacy middleware claim
                new Claim(ClaimTypes.Name, account.Username),                            // username
                new Claim(ClaimTypes.Role, roleName),                                    // role
                new Claim(JwtRegisteredClaimNames.Email, account.Email),                //email
                new Claim("fullName", account.FullName ?? ""),                           //fullname
                new Claim("avatarUrl", account.AvatarUrl ?? ""),
                new Claim("isVerified", (account.Status != AccountStatusEnum.EmailNotVerified).ToString())

            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(accessTokenMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        //no use
        public string? ValidateToken(string token)
        {
            if (token == null) return null;

            var jwtSettings = _config.GetSection("Jwt");
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                return jwtToken.Claims.First(x => x.Type == "id").Value;
            }
            catch
            {
                return null;
            }
        }
    }
}
