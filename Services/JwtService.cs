using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ecommerce_Backend.Models;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce_Backend.Services
{
    public interface IJwtService
    {
        string GenerateToken(UserLoginResponse user);
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(UserLoginResponse user)
        {
            var key     = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddDays(
                            int.Parse(_configuration["Jwt:ExpireDays"]!));

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.id.ToString()),
                new Claim(ClaimTypes.Email,          user.email ?? ""),
                new Claim(ClaimTypes.GivenName,      user.first_name ?? ""),
                new Claim(ClaimTypes.Surname,        user.last_name ?? ""),
                new Claim(ClaimTypes.Role,           user.role ?? ""),
            };

            var token = new JwtSecurityToken(
                issuer:             _configuration["Jwt:Issuer"],
                audience:           _configuration["Jwt:Audience"],
                claims:             claims,
                expires:            expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}