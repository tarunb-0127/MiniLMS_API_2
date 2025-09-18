using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Mini_LMS.Helpers
{
    public class JwtHelper
    {
        private readonly IConfiguration _config;

        public JwtHelper(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Generate a JWT including:
        /// - ClaimTypes.Email so your controller’s User.FindFirst(ClaimTypes.Email) works
        /// - JwtRegisteredClaimNames.Email for standard JWT tools
        /// - ClaimTypes.Role for ASP-NET’s [Authorize(Roles="…")] filter
        /// - Custom "UserId" and JTI
        /// </summary>
        public string GenerateToken(string email, string role, int userId)
        {
            var claims = new List<Claim>
            {
                // Standard subject
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),

                // Explicit email claims
                new Claim(ClaimTypes.Email, email),
                new Claim(JwtRegisteredClaimNames.Email, email),

                // Role claim for ASP-NET authorization
                new Claim(ClaimTypes.Role, role),
                // Optional: duplicate the role if your JWT middleware expects "role"
                new Claim("role", role),

                // Custom user ID claim
                new Claim("UserId", userId.ToString()),

                // Unique token identifier
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"])
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(5),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// If you ever support multiple roles per user, call this overload:
        /// </summary>
        public string GenerateToken(string email, IEnumerable<string> roles, int userId)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim("UserId", userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Emit one ClaimTypes.Role per role
            foreach (var r in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, r));
            }

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"])
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(5),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
