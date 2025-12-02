// TokenService.cs
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace E_Commerce_Admin_Panel.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;
        public TokenService(IConfiguration config) => _config = config;

        public string CreateToken(long userId, string userName, IEnumerable<string> permissions, IEnumerable<string> roles)
        {
            var key = _config["Jwt:Key"];
            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];
            var expiresMinutes = int.TryParse(_config["Jwt:ExpiresMinutes"], out var m) ? m : 120;

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),

                // MOST IMPORTANT CLAIM FOR ASP.NET IDENTITY -> maps to User.Identity.Name
                new Claim(ClaimTypes.Name, userName),

                // optional but useful
                new Claim("username", userName)
            };

            // ---- Add permission claims safely ----
            if (permissions != null)
            {
                claims.AddRange(permissions
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct()
                    .Select(p => new Claim("permission", p)));
            }

            // ---- Add roles (ASP.NET recognizes ClaimTypes.Role) ----
            if (roles != null)
            {
                claims.AddRange(roles
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct()
                    .Select(r => new Claim(ClaimTypes.Role, r)));
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
