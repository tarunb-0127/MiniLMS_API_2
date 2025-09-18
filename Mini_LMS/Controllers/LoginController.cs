using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Mini_LMS.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly MiniLMSContext _db;

    public AuthController(IConfiguration config, MiniLMSContext db)
    {
        _config = config;
        _db = db;
    }

    [HttpPost("login")]
    public IActionResult Login(
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string role)
    {
        // 1) Verify credentials
        var user = _db.Users
            .FirstOrDefault(u => u.Email == email && u.Role == role);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid credentials" });

        // 2) Load JWT settings
        var jwtSection = _config.GetSection("Jwt");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        var secret = jwtSection["Key"];

        // key must be >= 256 bits (32 bytes)
        var keyBytes = Encoding.UTF8.GetBytes(secret);

        // 3) Create signing credentials
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256Signature
        );

        // 4) Build the token
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: new[]
            {
                 new Claim("username", user.Username), 
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Role, user.Role),
        new Claim("email", user.Email),       //
        new Claim("UserId", user.Id.ToString()) // 
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        // 5) Return the serialized token
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { token = tokenString, role = user.Role });
    }
}
